namespace SemanticMapper.Internal.Planning;

/// <summary>
/// Turns the candidate matrix into one-to-one decisions and applies the confidence policy.
/// </summary>
/// <remarks>
/// <para>
/// Assignment is greedy over the whole matrix: all (source, target) pairs are taken in descending
/// confidence order (ties by document order, then candidate order) and a pair is assigned when both
/// the source and the target are still free. A source whose preferred target is taken by a stronger
/// pair falls back to its next candidate, which is then judged by the policy on its own merits.
/// </para>
/// <para>
/// The confidence gap is measured against the next-best candidate that is not assigned to another
/// source, so a candidate that is confidently claimed elsewhere does not make a match ambiguous.
/// </para>
/// <para>
/// Sources rejected with <see cref="MappingFailureBehavior.LeaveDefault"/> release their target and the
/// assignment is recomputed without them until it is stable. Each round removes at least one source,
/// so this terminates.
/// </para>
/// </remarks>
internal static class AssignmentSolver
{
    // Absorbs floating-point noise so that, for example, 0.85 - 0.75 satisfies a 0.10 gap.
    private const double Epsilon = 1e-9;

    public static IReadOnlyList<FieldMappingDecision> Solve(IReadOnlyList<MatchRow> rows, SemanticMapperOptions options)
    {
        var decisions = new PendingDecision?[rows.Count];
        var active = new HashSet<int>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].IsNoMatch)
            {
                decisions[i] = new PendingDecision(null, null, null, MatchStatus.NoMatch, DecisionOutcome.Unmapped, null);
            }
            else
            {
                active.Add(i);
            }
        }

        Dictionary<int, CandidateScore> assignment;
        while (true)
        {
            assignment = Assign(rows, active);
            var claimedBy = assignment.ToDictionary(p => p.Value.Target.Path, p => p.Key, StringComparer.Ordinal);
            var released = false;

            foreach (var i in active.ToList())
            {
                var decision = Evaluate(i, rows[i], assignment, claimedBy, options);
                decisions[i] = decision;
                if (decision.Outcome is DecisionOutcome.LeftDefault)
                {
                    active.Remove(i);
                    released = true;
                }
            }

            if (!released)
            {
                break;
            }
        }

        // Report which other source finally holds each candidate.
        var finalClaims = assignment
            .Where(p => decisions[p.Key]!.Outcome is DecisionOutcome.Applied or DecisionOutcome.Failed)
            .ToDictionary(p => p.Value.Target.Path, p => rows[p.Key].Source.Path, StringComparer.Ordinal);

        return [.. rows.Select((row, i) => ToDecision(row, decisions[i]!, finalClaims, options))];
    }

    private static Dictionary<int, CandidateScore> Assign(IReadOnlyList<MatchRow> rows, HashSet<int> active)
    {
        var pairs = active
            .SelectMany(i => rows[i].Candidates.Select((c, rank) => (Source: i, Rank: rank, Score: c)))
            .Where(p => p.Score.Confidence > 0)
            .OrderByDescending(p => p.Score.Confidence)
            .ThenBy(p => p.Source)
            .ThenBy(p => p.Rank);

        var assignment = new Dictionary<int, CandidateScore>();
        var takenTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            if (!assignment.ContainsKey(pair.Source) && takenTargets.Add(pair.Score.Target.Path))
            {
                assignment[pair.Source] = pair.Score;
            }
        }

        return assignment;
    }

    private static PendingDecision Evaluate(
        int index,
        MatchRow row,
        Dictionary<int, CandidateScore> assignment,
        Dictionary<string, int> claimedBy,
        SemanticMapperOptions options)
    {
        if (!assignment.TryGetValue(index, out var selected))
        {
            return new PendingDecision(null, null, null, MatchStatus.Conflict, DecisionOutcome.Unmapped, null);
        }

        var runnerUp = row.Candidates
            .Where(c => c.Target.Path != selected.Target.Path)
            .Where(c => !claimedBy.TryGetValue(c.Target.Path, out var owner) || owner == index)
            .Select(c => c.Confidence)
            .DefaultIfEmpty(0)
            .Max();
        var gap = selected.Confidence - runnerUp;

        var (status, behavior) =
            selected.Confidence + Epsilon < options.MinimumConfidence ? (MatchStatus.LowConfidence, options.OnLowConfidence)
            : gap + Epsilon < options.MinimumConfidenceGap ? (MatchStatus.Ambiguous, options.OnAmbiguousMatch)
            : (MatchStatus.Safe, (MappingFailureBehavior?)null);

        var outcome = behavior switch
        {
            null or MappingFailureBehavior.UseBestMatch => DecisionOutcome.Applied,
            MappingFailureBehavior.LeaveDefault => DecisionOutcome.LeftDefault,
            _ => DecisionOutcome.Failed,
        };

        return new PendingDecision(selected.Target.Path, selected.Confidence, gap, status, outcome, behavior);
    }

    private static FieldMappingDecision ToDecision(
        MatchRow row,
        PendingDecision pending,
        Dictionary<string, string> claims,
        SemanticMapperOptions options) => new()
        {
            SourcePath = row.Source.Path,
            SourceName = row.Source.Name,
            SourceValueKind = row.Source.ValueKind,
            SourceIsArray = row.Source.IsArray,
            Candidates = [.. row.Candidates.Select(c => new CandidateDiagnostic
            {
                TargetPath = c.Target.Path,
                Confidence = c.Confidence,
                ClaimedBySourcePath = claims.TryGetValue(c.Target.Path, out var owner) && owner != row.Source.Path ? owner : null,
            })],
            NoMatchConfidence = row.NoMatchConfidence,
            SelectedTargetPath = pending.TargetPath,
            SelectedConfidence = pending.Confidence,
            ConfidenceGap = pending.Gap,
            MinimumConfidence = options.MinimumConfidence,
            MinimumConfidenceGap = options.MinimumConfidenceGap,
            Status = pending.Status,
            Outcome = pending.Outcome,
            AppliedBehavior = pending.Behavior,
        };

    private sealed record PendingDecision(
        string? TargetPath,
        double? Confidence,
        double? Gap,
        MatchStatus Status,
        DecisionOutcome Outcome,
        MappingFailureBehavior? Behavior);
}
