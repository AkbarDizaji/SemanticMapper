using System.Collections.Concurrent;

namespace SemanticMapper.Core.Tests.Infrastructure;

/// <summary>
/// Returns pre-configured scores keyed by source path and target path, and records every call.
/// Unconfigured source fields get an empty result (no candidates).
/// </summary>
public sealed class FakeSemanticFieldMatcher : ISemanticFieldMatcher
{
    private readonly Dictionary<string, (Dictionary<string, double> Scores, double? NoMatch)> _scores = new(StringComparer.Ordinal);

    public string Version { get; set; } = "fake-1";

    public ConcurrentQueue<SourceField> Calls { get; } = new();

    public ConcurrentQueue<IReadOnlyList<TargetField>> CandidateLists { get; } = new();

    public ConcurrentQueue<CancellationToken> Tokens { get; } = new();

    /// <summary>Optional hook run on every call, e.g. to block until cancelled.</summary>
    public Func<SourceField, CancellationToken, Task>? OnMatch { get; set; }

    public int CallCount => Calls.Count;

    public FakeSemanticFieldMatcher Score(string sourcePath, params (string Target, double Confidence)[] scores)
    {
        _scores[sourcePath] = (scores.ToDictionary(s => s.Target, s => s.Confidence, StringComparer.Ordinal), null);
        return this;
    }

    public FakeSemanticFieldMatcher ScoreWithNoMatch(string sourcePath, double noMatch, params (string Target, double Confidence)[] scores)
    {
        _scores[sourcePath] = (scores.ToDictionary(s => s.Target, s => s.Confidence, StringComparer.Ordinal), noMatch);
        return this;
    }

    public async Task<FieldMatchResult> MatchAsync(
        SourceField source,
        IReadOnlyList<TargetField> candidates,
        CancellationToken cancellationToken = default)
    {
        Calls.Enqueue(source);
        CandidateLists.Enqueue(candidates);
        Tokens.Enqueue(cancellationToken);
        if (OnMatch is not null)
        {
            await OnMatch(source, cancellationToken);
        }

        if (!_scores.TryGetValue(source.Path, out var configured))
        {
            return new FieldMatchResult([]);
        }

        var scored = candidates
            .Where(c => configured.Scores.ContainsKey(c.Path))
            .Select(c => new CandidateScore(c, configured.Scores[c.Path]));
        return new FieldMatchResult(scored, configured.NoMatch);
    }
}
