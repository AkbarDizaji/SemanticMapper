using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace SemanticMapper.Jev;

/// <summary>
/// An <see cref="ISemanticFieldMatcher"/> backed by the TypeSafe Jev model.
/// </summary>
/// <remarks>
/// <para>
/// Each source field becomes one TypeSafe System One <c>choice</c> question whose options are the
/// destination candidates plus a "none" option. The returned probabilities become the candidate
/// confidences, and the "none" probability becomes <see cref="FieldMatchResult.NoMatchConfidence"/>.
/// </para>
/// <para>
/// Requests go directly from your application to TypeSafe using your own API key. By default only
/// field names, paths and inferred kinds are sent; see <see cref="JevMatcherOptions.IncludeSourceValues"/>.
/// </para>
/// </remarks>
public sealed partial class JevSemanticFieldMatcher : ISemanticFieldMatcher
{
    /// <summary>Bump when the question wording or request shape changes, so old cached plans are not reused.</summary>
    private const string PromptVersion = "p1";
    private const string QuestionId = "destination_property";
    private const int MaxChoiceOptions = 255;

    private const string Instructions =
        "The state describes one field from an external JSON or XML document. Decide which destination " +
        "property should receive that field's value. Judge by meaning: what real-world information the field " +
        "represents. Field names may be abbreviated, use another naming convention, or use another language, so " +
        "do not rely on spelling similarity. Use the field path for context and the value kind for compatibility. " +
        "Choose the none option if no property represents the same information.";

    private readonly Func<HttpClient> _createClient;
    private readonly JevMatcherOptions _options;
    private readonly string? _apiKey;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance that obtains clients from an <see cref="IHttpClientFactory"/>.</summary>
    /// <param name="httpClientFactory">The factory. The client named <see cref="JevDefaults.HttpClientName"/> is used.</param>
    /// <param name="options">The provider options.</param>
    /// <param name="logger">The logger.</param>
    public JevSemanticFieldMatcher(
        IHttpClientFactory httpClientFactory,
        IOptions<JevMatcherOptions> options,
        ILogger<JevSemanticFieldMatcher>? logger = null)
        : this(() => httpClientFactory.CreateClient(JevDefaults.HttpClientName), options?.Value!, logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
    }

    /// <summary>Initializes a new instance that uses the given <see cref="HttpClient"/>.</summary>
    /// <param name="httpClient">The HTTP client. It is not disposed by the matcher.</param>
    /// <param name="options">The provider options.</param>
    /// <param name="logger">The logger.</param>
    public JevSemanticFieldMatcher(HttpClient httpClient, JevMatcherOptions options, ILogger<JevSemanticFieldMatcher>? logger = null)
        : this(() => httpClient, options, logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    private JevSemanticFieldMatcher(Func<HttpClient> createClient, JevMatcherOptions options, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.BaseAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Model);
        if (options.Timeout <= TimeSpan.Zero && options.Timeout != System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Timeout, $"{nameof(JevMatcherOptions.Timeout)} must be positive.");
        }

        _createClient = createClient;
        _options = options;
        _apiKey = JevApiKeyResolver.Resolve(options.ApiKey, configuration: null, Environment.GetEnvironmentVariable);
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public string Version => $"typesafe-jev:{_options.Model}:{PromptVersion}";

    /// <inheritdoc />
    /// <exception cref="JevAuthenticationException">No API key is configured, or TypeSafe rejected it.</exception>
    /// <exception cref="JevApiException">TypeSafe returned an error or an unexpected response.</exception>
    public async Task<FieldMatchResult> MatchAsync(
        SourceField source,
        IReadOnlyList<TargetField> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidates);
        cancellationToken.ThrowIfCancellationRequested();

        if (_apiKey is null)
        {
            throw new JevAuthenticationException(
                $"No TypeSafe API key is configured for the Jev semantic matcher. Set {nameof(JevMatcherOptions)}.{nameof(JevMatcherOptions.ApiKey)}, " +
                $"the configuration key '{JevDefaults.ApiKeyConfigurationKey}', or the environment variable '{JevDefaults.ApiKeyEnvironmentVariable}'.");
        }

        if (candidates.Count == 0)
        {
            return FieldMatchResult.NoMatch;
        }

        if (candidates.Count >= MaxChoiceOptions)
        {
            throw new JevException(
                $"The destination type has {candidates.Count} candidate properties; the Jev provider supports at most {MaxChoiceOptions - 1}.");
        }

        var labels = candidates.Select(c => c.Path).ToList();
        var noneLabel = CreateNoneLabel(labels);
        var body = BuildRequest(source, candidates, noneLabel);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.BaseAddress, "v1/systemone"))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _createClient().SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogRequestFailed(_logger, source.Path, null, null);
            throw new JevApiException($"The TypeSafe request timed out after {_options.Timeout.TotalSeconds:0.#} seconds.", null, null, ex);
        }
        catch (HttpRequestException ex)
        {
            LogRequestFailed(_logger, source.Path, null, null);
            throw new JevApiException("The TypeSafe API could not be reached.", null, null, ex);
        }

        using (response)
        {
            var requestId = GetRequestId(response);
            if (!response.IsSuccessStatusCode)
            {
                LogRequestFailed(_logger, source.Path, (int)response.StatusCode, requestId);
                throw CreateError(response.StatusCode, requestId);
            }

            string json;
            try
            {
                json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new JevApiException("Timed out while reading the TypeSafe response.", response.StatusCode, requestId, ex);
            }

            var result = ParseResponse(json, candidates, noneLabel, response.StatusCode, requestId);
            LogRequestCompleted(_logger, source.Path, candidates.Count, stopwatch.ElapsedMilliseconds, requestId);
            return result;
        }
    }

    private string BuildRequest(SourceField source, IReadOnlyList<TargetField> candidates, string noneLabel)
    {
        var field = new JsonObject
        {
            ["name"] = source.Name,
            ["path"] = source.Path,
            ["value_kind"] = Describe(source.ValueKind),
            ["is_list"] = source.IsArray,
        };
        if (_options.IncludeSourceValues)
        {
            field["value"] = source.IsArray
                ? new JsonArray([.. source.Items.Select(i => (JsonNode?)JsonValue.Create(i))])
                : JsonValue.Create(source.Value);
        }

        var destinationType = candidates.FirstOrDefault(c => !c.Path.Contains('.', StringComparison.Ordinal))?.DeclaringTypeName
            ?? candidates[0].DeclaringTypeName;

        var criteria = new JsonObject();
        foreach (var candidate in candidates)
        {
            criteria[candidate.Path] = DescribeCandidate(candidate);
        }

        criteria[noneLabel] = "None of the properties above represents the same information as the field.";

        var root = new JsonObject
        {
            ["model"] = _options.Model,
            ["state"] = new JsonObject
            {
                ["source_field"] = field,
                ["destination_type"] = destinationType,
            },
            ["questions"] = new JsonObject
            {
                [QuestionId] = new JsonObject
                {
                    ["type"] = "choice",
                    ["instructions"] = Instructions,
                    ["criteria"] = criteria,
                },
            },
        };
        return root.ToJsonString();
    }

    private static FieldMatchResult ParseResponse(
        string json,
        IReadOnlyList<TargetField> candidates,
        string noneLabel,
        HttpStatusCode statusCode,
        string? requestId)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("answers", out var answers)
                || !answers.TryGetProperty(QuestionId, out var answer))
            {
                throw new JevApiException("The TypeSafe response did not contain an answer to the matching question.", statusCode, requestId);
            }

            var probabilities = new Dictionary<string, double>(StringComparer.Ordinal);
            if (answer.TryGetProperty("probabilities", out var probabilitiesElement) && probabilitiesElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in probabilitiesElement.EnumerateObject())
                {
                    if (property.Value.TryGetDouble(out var p))
                    {
                        probabilities[property.Name] = Math.Clamp(p, 0, 1);
                    }
                }
            }
            else if (answer.TryGetProperty("choice", out var choice) && choice.ValueKind == JsonValueKind.String
                && answer.TryGetProperty("confidence", out var confidence) && confidence.TryGetDouble(out var c))
            {
                probabilities[choice.GetString()!] = Math.Clamp(c, 0, 1);
            }
            else
            {
                throw new JevApiException("The TypeSafe response did not contain choice probabilities.", statusCode, requestId);
            }

            var scores = candidates
                .Where(t => probabilities.ContainsKey(t.Path))
                .Select(t => new CandidateScore(t, probabilities[t.Path]));
            return new FieldMatchResult(scores, probabilities.TryGetValue(noneLabel, out var none) ? none : null);
        }
        catch (JsonException ex)
        {
            throw new JevApiException("The TypeSafe response was not valid JSON.", statusCode, requestId, ex);
        }
    }

    private static JevException CreateError(HttpStatusCode statusCode, string? requestId) => statusCode switch
    {
        HttpStatusCode.Unauthorized => new JevAuthenticationException("TypeSafe rejected the API key (HTTP 401). Check that the key is valid.", statusCode, requestId),
        HttpStatusCode.Forbidden => new JevAuthenticationException("The TypeSafe API key is not permitted to use this model or endpoint (HTTP 403).", statusCode, requestId),
        HttpStatusCode.TooManyRequests => new JevApiException("TypeSafe rate limit exceeded (HTTP 429). Retry later with backoff.", statusCode, requestId),
        _ => new JevApiException(
            string.Create(CultureInfo.InvariantCulture, $"TypeSafe returned HTTP {(int)statusCode}{(requestId is null ? "" : $" (request {requestId})")}."),
            statusCode,
            requestId),
    };

    private static string? GetRequestId(HttpResponseMessage response)
    {
        foreach (var header in new[] { "x-request-id", "request-id" })
        {
            if (response.Headers.TryGetValues(header, out var values))
            {
                return values.FirstOrDefault();
            }
        }

        return null;
    }

    private static string CreateNoneLabel(List<string> labels)
    {
        var label = "none";
        while (labels.Contains(label, StringComparer.OrdinalIgnoreCase))
        {
            label = "_" + label;
        }

        return label;
    }

    private static string DescribeCandidate(TargetField candidate)
    {
        var builder = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"Property '{candidate.Name}' of {candidate.DeclaringTypeName}; ")
            .Append(candidate.IsCollection ? "a list of " : "")
            .Append(Describe(candidate.ValueKind));
        if (candidate.PropertyType.IsEnum || Nullable.GetUnderlyingType(candidate.PropertyType)?.IsEnum == true)
        {
            var enumType = Nullable.GetUnderlyingType(candidate.PropertyType) ?? candidate.PropertyType;
            builder.Append(" (one of: ").AppendJoin(", ", Enum.GetNames(enumType)).Append(')');
        }

        return builder.ToString();
    }

    private static string Describe(FieldValueKind kind) => kind switch
    {
        FieldValueKind.String => "text",
        FieldValueKind.Boolean => "true/false value",
        FieldValueKind.Integer => "whole number",
        FieldValueKind.Decimal => "number",
        FieldValueKind.Date => "date",
        FieldValueKind.DateTime => "date and time",
        FieldValueKind.Time => "time of day",
        FieldValueKind.Duration => "duration",
        FieldValueKind.Guid => "unique identifier",
        FieldValueKind.Enumeration => "named value",
        FieldValueKind.Null => "empty value",
        _ => "value",
    };

    [LoggerMessage(EventId = 100, EventName = "JevRequestCompleted", Level = LogLevel.Debug,
        Message = "TypeSafe Jev scored {CandidateCount} candidates for source field {SourcePath} in {ElapsedMilliseconds} ms (request {RequestId})")]
    private static partial void LogRequestCompleted(ILogger logger, string sourcePath, int candidateCount, long elapsedMilliseconds, string? requestId);

    [LoggerMessage(EventId = 101, EventName = "JevRequestFailed", Level = LogLevel.Warning,
        Message = "TypeSafe Jev request for source field {SourcePath} failed (status {StatusCode}, request {RequestId})")]
    private static partial void LogRequestFailed(ILogger logger, string sourcePath, int? statusCode, string? requestId);
}
