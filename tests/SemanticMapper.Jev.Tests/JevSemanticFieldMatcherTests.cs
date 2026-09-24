using System.Net;
using System.Text.Json;

namespace SemanticMapper.Jev.Tests;

public class JevSemanticFieldMatcherTests
{
    private const string ApiKey = "tsk_test_secret_key";
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static readonly TargetField[] Candidates =
    [
        new("FirstName", "FirstName", typeof(string), FieldValueKind.String, false, false, "Customer"),
        new("BirthDate", "BirthDate", typeof(DateTime), FieldValueKind.DateTime, false, false, "Customer"),
        new("Address.City", "City", typeof(string), FieldValueKind.String, false, true, "Address"),
    ];

    private static readonly SourceField Dob = new("person.dob", "dob", FieldValueKind.Date, "1993-06-10");

    private const string SuccessBody = """
        {
          "model": "jev-1.13.0",
          "answers": {
            "destination_property": {
              "type": "choice",
              "choice": "BirthDate",
              "probabilities": { "FirstName": 0.01, "BirthDate": 0.95, "Address.City": 0.01, "none": 0.03 },
              "confidence": 0.95
            }
          },
          "usage": { "input_tokens": 120, "output_tokens": 1 }
        }
        """;

    private static JevSemanticFieldMatcher CreateMatcher(StubHttpMessageHandler handler, Action<JevMatcherOptions>? configure = null)
    {
        var options = new JevMatcherOptions { ApiKey = ApiKey };
        configure?.Invoke(options);
        return new JevSemanticFieldMatcher(new HttpClient(handler), options);
    }

    [Fact]
    public async Task Sends_a_choice_question_with_every_candidate_and_a_none_option()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SuccessBody);
        var matcher = CreateMatcher(handler, o => o.Model = "jev-1.13.0");

        await matcher.MatchAsync(Dob, Candidates, Ct);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://api.typesafe.ai/v1/systemone"), request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(ApiKey, request.Headers.Authorization.Parameter);

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal("jev-1.13.0", root.GetProperty("model").GetString());
        var field = root.GetProperty("state").GetProperty("source_field");
        Assert.Equal("dob", field.GetProperty("name").GetString());
        Assert.Equal("person.dob", field.GetProperty("path").GetString());
        Assert.Equal("date", field.GetProperty("value_kind").GetString());
        Assert.Equal("Customer", root.GetProperty("state").GetProperty("destination_type").GetString());

        var question = root.GetProperty("questions").GetProperty("destination_property");
        Assert.Equal("choice", question.GetProperty("type").GetString());
        var labels = question.GetProperty("criteria").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["FirstName", "BirthDate", "Address.City", "none"], labels);
    }

    [Fact]
    public async Task Source_values_are_not_sent_by_default()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SuccessBody);

        await CreateMatcher(handler).MatchAsync(Dob, Candidates, Ct);

        Assert.DoesNotContain("1993-06-10", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Source_values_are_sent_when_opted_in()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SuccessBody);

        await CreateMatcher(handler, o => o.IncludeSourceValues = true).MatchAsync(Dob, Candidates, Ct);

        Assert.Contains("1993-06-10", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Probabilities_become_candidate_scores_and_none_becomes_no_match_confidence()
    {
        var matcher = CreateMatcher(new StubHttpMessageHandler(HttpStatusCode.OK, SuccessBody));

        var result = await matcher.MatchAsync(Dob, Candidates, Ct);

        Assert.Equal("BirthDate", result.Candidates[0].Target.Path);
        Assert.Same(Candidates[1], result.Candidates[0].Target);
        Assert.Equal(0.95, result.Candidates[0].Confidence);
        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(0.03, result.NoMatchConfidence);
    }

    [Fact]
    public async Task None_label_does_not_collide_with_a_property_named_none()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """
            { "answers": { "destination_property": { "probabilities": { "None": 0.9, "_none": 0.1 } } } }
            """);
        TargetField[] candidates = [new("None", "None", typeof(string), FieldValueKind.String, false, true, "T")];

        var result = await CreateMatcher(handler).MatchAsync(Dob, candidates, Ct);

        Assert.Equal(0.9, result.Candidates.Single().Confidence);
        Assert.Equal(0.1, result.NoMatchConfidence);
    }

    [Fact]
    public void Version_identifies_provider_model_and_prompt()
    {
        var a = CreateMatcher(new StubHttpMessageHandler(HttpStatusCode.OK, ""), o => o.Model = "jev-1.13.0");
        var b = CreateMatcher(new StubHttpMessageHandler(HttpStatusCode.OK, ""), o => o.Model = "jev-1.14.0");

        Assert.Contains("jev-1.13.0", a.Version, StringComparison.Ordinal);
        Assert.NotEqual(a.Version, b.Version);
    }

    [Fact]
    public async Task Missing_api_key_throws_a_provider_specific_exception_without_calling_the_api()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TYPESAFE_API_KEY")))
        {
            Assert.Skip("TYPESAFE_API_KEY is set in this environment.");
        }

        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, SuccessBody);
        var matcher = new JevSemanticFieldMatcher(new HttpClient(handler), new JevMatcherOptions());

        var ex = await Assert.ThrowsAsync<JevAuthenticationException>(() => matcher.MatchAsync(Dob, Candidates, Ct));

        Assert.Contains("TYPESAFE_API_KEY", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Rejected_key_throws_authentication_exception_without_leaking_the_key(HttpStatusCode status)
    {
        var matcher = CreateMatcher(new StubHttpMessageHandler(status, """{ "error": "invalid key" }""", "req_123"));

        var ex = await Assert.ThrowsAsync<JevAuthenticationException>(() => matcher.MatchAsync(Dob, Candidates, Ct));

        Assert.Equal(status, ex.StatusCode);
        Assert.Equal("req_123", ex.RequestId);
        Assert.DoesNotContain(ApiKey, ex.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.UnprocessableEntity, false)]
    [InlineData((HttpStatusCode)529, true)]
    public async Task Api_errors_throw_JevApiException_with_status(HttpStatusCode status, bool transient)
    {
        var matcher = CreateMatcher(new StubHttpMessageHandler(status, """{ "error": "x" }"""));

        var ex = await Assert.ThrowsAsync<JevApiException>(() => matcher.MatchAsync(Dob, Candidates, Ct));

        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(transient, ex.IsTransient);
        Assert.IsAssignableFrom<SemanticMatcherException>(ex);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "answers": {} }""")]
    [InlineData("""{ "answers": { "destination_property": { "type": "choice" } } }""")]
    public async Task Malformed_responses_throw_JevApiException(string body)
    {
        var matcher = CreateMatcher(new StubHttpMessageHandler(HttpStatusCode.OK, body));

        await Assert.ThrowsAsync<JevApiException>(() => matcher.MatchAsync(Dob, Candidates, Ct));
    }

    [Fact]
    public async Task Network_failures_throw_JevApiException()
    {
        var matcher = CreateMatcher(new StubHttpMessageHandler((_, _) => throw new HttpRequestException("connection refused")));

        var ex = await Assert.ThrowsAsync<JevApiException>(() => matcher.MatchAsync(Dob, Candidates, Ct));

        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task Request_timeout_throws_JevApiException_rather_than_cancellation()
    {
        var matcher = CreateMatcher(
            new StubHttpMessageHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("unreachable");
            }),
            o => o.Timeout = TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<JevApiException>(() => matcher.MatchAsync(Dob, Candidates, Ct));
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_to_the_http_request()
    {
        using var cts = new CancellationTokenSource();
        var observed = false;
        var matcher = CreateMatcher(new StubHttpMessageHandler(async (_, token) =>
        {
            await cts.CancelAsync();
            observed = token.IsCancellationRequested;
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("unreachable");
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => matcher.MatchAsync(Dob, Candidates, cts.Token));
        Assert.True(observed);
    }
}
