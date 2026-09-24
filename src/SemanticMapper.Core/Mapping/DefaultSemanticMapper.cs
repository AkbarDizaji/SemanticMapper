using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SemanticMapper.Internal.Conversion;
using SemanticMapper.Internal.Logging;
using SemanticMapper.Internal.Parsing;
using SemanticMapper.Internal.Planning;
using SemanticMapper.Internal.Schema;

namespace SemanticMapper;

/// <summary>
/// The default <see cref="ISemanticMapper"/>: parses the document, reuses or builds a mapping plan,
/// and materializes the destination object.
/// </summary>
/// <remarks>Instances are thread-safe and intended to be used as singletons.</remarks>
public sealed class DefaultSemanticMapper : ISemanticMapper
{
    private readonly ISemanticFieldMatcher _matcher;
    private readonly IMappingPlanCache _cache;
    private readonly SemanticMapperOptions _options;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Type, TargetSchema> _schemas = new();

    /// <summary>Initializes a new instance.</summary>
    /// <param name="matcher">The semantic matcher used on plan-cache misses.</param>
    /// <param name="options">The mapper options. Defaults are used when omitted.</param>
    /// <param name="cache">The plan cache. A private <see cref="InMemoryMappingPlanCache"/> is used when omitted.</param>
    /// <param name="logger">The logger.</param>
    public DefaultSemanticMapper(
        ISemanticFieldMatcher matcher,
        IOptions<SemanticMapperOptions>? options = null,
        IMappingPlanCache? cache = null,
        ILogger<DefaultSemanticMapper>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        _matcher = matcher;
        _options = Snapshot(options?.Value ?? new SemanticMapperOptions());
        _cache = cache ?? new InMemoryMappingPlanCache();
        _logger = logger ?? (ILogger)NullLogger.Instance;

        var validation = new SemanticMapperOptionsValidator().Validate(null, _options);
        if (validation.Failed)
        {
            throw new OptionsValidationException(nameof(SemanticMapperOptions), typeof(SemanticMapperOptions), validation.Failures);
        }
    }

    /// <inheritdoc />
    public async Task<MappingResult<T>> MapAsync<T>(string jsonOrXml, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsonOrXml);
        cancellationToken.ThrowIfCancellationRequested();

        var document = DocumentParser.Parse(jsonOrXml, _options.MaxDepth);
        foreach (var path in document.UnsupportedPaths)
        {
            Log.UnsupportedSourceField(_logger, path);
        }

        var schema = _schemas.GetOrAdd(typeof(T), static (type, maxDepth) => TargetSchemaBuilder.Build(type, maxDepth), _options.MaxDepth);
        var key = PlanKeyFactory.Create(document.Fields, schema.Fingerprint, _matcher.Version, _options);
        var destinationName = typeof(T).FullName ?? typeof(T).Name;

        var plan = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        var usedCachedPlan = plan is not null;
        if (plan is not null)
        {
            Log.PlanCacheHit(_logger, destinationName, key.Value);
        }
        else
        {
            Log.PlanCacheMiss(_logger, destinationName, key.Value, document.Fields.Count, _matcher.Version);
            plan = await BuildPlanAsync(document, schema, key, cancellationToken).ConfigureAwait(false);
            await _cache.SetAsync(plan, cancellationToken).ConfigureAwait(false);
        }

        var value = Materialize<T>(document, schema, plan);
        var diagnostics = CreateDiagnostics(document, schema, key, plan.MatcherVersion, plan.Decisions, plan.UnmappedTargetPaths);
        return new MappingResult<T>(value, diagnostics, usedCachedPlan);
    }

    private async Task<MappingPlan> BuildPlanAsync(SourceDocument document, TargetSchema schema, MappingPlanKey key, CancellationToken cancellationToken)
    {
        var scalarCandidates = schema.Fields.Where(f => !f.IsCollection).ToList();
        var collectionCandidates = schema.Fields.Where(f => f.IsCollection).ToList();
        var rows = new MatchRow[document.Fields.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, document.Fields.Count),
            new ParallelOptions { MaxDegreeOfParallelism = _options.MaxConcurrentMatches, CancellationToken = cancellationToken },
            async (index, ct) =>
            {
                var source = document.Fields[index];
                var candidates = source.IsArray ? collectionCandidates : scalarCandidates;
                rows[index] = candidates.Count == 0
                    ? new MatchRow(source, [], null)
                    : await MatchAsync(source, candidates, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

        var decisions = AssignmentSolver.Solve(rows, _options);
        LogDecisions(decisions);

        var unmapped = UnmappedTargets(schema, decisions);
        if (decisions.Any(d => d.Outcome == DecisionOutcome.Failed))
        {
            throw new UnsafeMappingException(CreateDiagnostics(document, schema, key, _matcher.Version, decisions, unmapped));
        }

        return new MappingPlan
        {
            Key = key,
            DestinationTypeName = schema.Type.FullName ?? schema.Type.Name,
            MatcherVersion = _matcher.Version,
            Decisions = decisions,
            UnmappedTargetPaths = unmapped,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private async Task<MatchRow> MatchAsync(SourceField source, List<TargetField> candidates, CancellationToken cancellationToken)
    {
        FieldMatchResult result;
        try
        {
            result = await _matcher.MatchAsync(source, candidates, cancellationToken).ConfigureAwait(false)
                ?? throw new SemanticMatcherException($"The semantic matcher returned no result for source field '{source.Path}'.");
        }
        catch (Exception ex) when (ex is not SemanticMapperException and not OperationCanceledException)
        {
            throw new SemanticMatcherException($"The semantic matcher failed for source field '{source.Path}'.", ex);
        }

        // Re-bind scores to our own candidate instances so matchers cannot introduce foreign targets.
        var byPath = candidates.ToDictionary(c => c.Path, StringComparer.Ordinal);
        var scores = new List<CandidateScore>(result.Candidates.Count);
        foreach (var score in result.Candidates)
        {
            if (!byPath.TryGetValue(score.Target.Path, out var target))
            {
                throw new SemanticMatcherException(
                    $"The semantic matcher scored '{score.Target.Path}' for source field '{source.Path}', which is not one of the offered candidates.");
            }

            scores.Add(score with { Target = target });
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var best = scores.Count > 0 ? scores[0] : default;
            Log.SemanticMatchPerformed(_logger, source.Path, candidates.Count, best.Target?.Path, best.Confidence);
        }

        return new MatchRow(source, scores, result.NoMatchConfidence);
    }

    private void LogDecisions(IReadOnlyList<FieldMappingDecision> decisions)
    {
        if (!_logger.IsEnabled(LogLevel.Debug) && !_logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        foreach (var decision in decisions)
        {
            var target = decision.SelectedTargetPath;
            switch (decision.Status)
            {
                case MatchStatus.NoMatch:
                    Log.NoMatch(_logger, decision.SourcePath);
                    continue;
                case MatchStatus.Conflict:
                    LogConflict(decision);
                    continue;
                case MatchStatus.LowConfidence:
                    Log.LowConfidence(_logger, decision.SourcePath, target!, decision.SelectedConfidence!.Value, decision.MinimumConfidence, decision.AppliedBehavior!.Value);
                    break;
                case MatchStatus.Ambiguous:
                    Log.Ambiguous(_logger, decision.SourcePath, target!, decision.SelectedConfidence!.Value, decision.ConfidenceGap!.Value, decision.MinimumConfidenceGap, decision.AppliedBehavior!.Value);
                    break;
            }

            if (decision.IsApplied)
            {
                Log.CandidateSelected(_logger, decision.SourcePath, target!, decision.SelectedConfidence!.Value, decision.ConfidenceGap!.Value, decision.Status);
            }
            else
            {
                Log.CandidateRejected(_logger, target!, decision.SourcePath, decision.Outcome == DecisionOutcome.Failed ? "mapping failed" : "left at default");
            }
        }
    }

    private void LogConflict(FieldMappingDecision decision)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        foreach (var candidate in decision.Candidates.Where(c => c.ClaimedBySourcePath is not null))
        {
#pragma warning disable CA1873 // Guarded by the IsEnabled check above.
            Log.CandidateRejected(_logger, candidate.TargetPath, decision.SourcePath, $"assigned to source field '{candidate.ClaimedBySourcePath}'");
#pragma warning restore CA1873
        }
    }

    private static T Materialize<T>(SourceDocument document, TargetSchema schema, MappingPlan plan)
    {
        var root = schema.CreateInstance();
        var sources = document.Fields.ToDictionary(f => f.Path, StringComparer.Ordinal);

        foreach (var decision in plan.Decisions)
        {
            if (!decision.IsApplied || !sources.TryGetValue(decision.SourcePath, out var source))
            {
                continue;
            }

            var member = schema.Find(decision.SelectedTargetPath!)
                ?? throw new SemanticMapperException($"The mapping plan refers to '{decision.SelectedTargetPath}', which does not exist on '{schema.Type.FullName}'.");

            if (ValueConverter.TryConvert(source, member, out var value))
            {
                Assign(root, member, value);
            }
        }

        return (T)root;
    }

    private static void Assign(object root, TargetMember member, object? value)
    {
        var current = root;
        for (var i = 0; i < member.Chain.Count - 1; i++)
        {
            var property = member.Chain[i];
            var next = property.GetValue(current);
            if (next is null)
            {
                next = Activator.CreateInstance(property.PropertyType)!;
                property.SetValue(current, next);
            }

            current = next;
        }

        member.Property.SetValue(current, value);
    }

    private static IReadOnlyList<string> UnmappedTargets(TargetSchema schema, IReadOnlyList<FieldMappingDecision> decisions)
    {
        var applied = decisions.Where(d => d.IsApplied).Select(d => d.SelectedTargetPath!).ToHashSet(StringComparer.Ordinal);
        return [.. schema.Fields.Select(f => f.Path).Where(p => !applied.Contains(p))];
    }

    private static MappingDiagnostics CreateDiagnostics(
        SourceDocument document,
        TargetSchema schema,
        MappingPlanKey key,
        string matcherVersion,
        IReadOnlyList<FieldMappingDecision> decisions,
        IReadOnlyList<string> unmappedTargets) => new()
        {
            SourceFormat = document.Format,
            DestinationType = schema.Type,
            PlanKey = key,
            MatcherVersion = matcherVersion,
            Decisions = decisions,
            UnmappedTargetPaths = unmappedTargets,
            UnsupportedSourcePaths = document.UnsupportedPaths,
        };

    // Options are copied so later mutation of the options object cannot desynchronize plan keys and decisions.
    private static SemanticMapperOptions Snapshot(SemanticMapperOptions options) => new()
    {
        MinimumConfidence = options.MinimumConfidence,
        MinimumConfidenceGap = options.MinimumConfidenceGap,
        OnLowConfidence = options.OnLowConfidence,
        OnAmbiguousMatch = options.OnAmbiguousMatch,
        MaxConcurrentMatches = options.MaxConcurrentMatches,
        MaxDepth = options.MaxDepth,
    };
}
