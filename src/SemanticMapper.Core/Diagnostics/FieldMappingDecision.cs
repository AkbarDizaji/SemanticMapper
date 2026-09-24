using System.Globalization;
using System.Text;

namespace SemanticMapper;

/// <summary>
/// The mapper's decision for a single source field, including every candidate score.
/// </summary>
/// <remarks>Decisions never contain source values, so they are safe to log.</remarks>
public sealed class FieldMappingDecision
{
    /// <summary>Gets the source field path.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Gets the source field name.</summary>
    public required string SourceName { get; init; }

    /// <summary>Gets the inferred kind of the source value.</summary>
    public required FieldValueKind SourceValueKind { get; init; }

    /// <summary>Gets a value indicating whether the source field is an array.</summary>
    public bool SourceIsArray { get; init; }

    /// <summary>Gets every scored candidate, ordered by descending confidence.</summary>
    public required IReadOnlyList<CandidateDiagnostic> Candidates { get; init; }

    /// <summary>Gets the matcher's confidence that the field has no counterpart, if reported.</summary>
    public double? NoMatchConfidence { get; init; }

    /// <summary>Gets the candidate chosen by the assignment (whether or not it was applied), if any.</summary>
    public string? SelectedTargetPath { get; init; }

    /// <summary>Gets the confidence of <see cref="SelectedTargetPath"/>.</summary>
    public double? SelectedConfidence { get; init; }

    /// <summary>Gets the difference between the selected candidate and the next-best available candidate.</summary>
    public double? ConfidenceGap { get; init; }

    /// <summary>Gets the confidence threshold that was applied.</summary>
    public required double MinimumConfidence { get; init; }

    /// <summary>Gets the confidence gap that was required.</summary>
    public required double MinimumConfidenceGap { get; init; }

    /// <summary>Gets how the selected match relates to the confidence rules.</summary>
    public required MatchStatus Status { get; init; }

    /// <summary>Gets what the mapper did with the field.</summary>
    public required DecisionOutcome Outcome { get; init; }

    /// <summary>Gets the failure behavior that decided the outcome, or <see langword="null"/> for safe and unmapped fields.</summary>
    public MappingFailureBehavior? AppliedBehavior { get; init; }

    /// <summary>Gets a value indicating whether the source value is written to <see cref="SelectedTargetPath"/>.</summary>
    public bool IsApplied => Outcome == DecisionOutcome.Applied;

    /// <summary>Returns a multi-line, human-readable description of the decision.</summary>
    public override string ToString()
    {
        var culture = CultureInfo.InvariantCulture;
        var builder = new StringBuilder();
        builder.Append(culture, $"Source: {SourcePath}").AppendLine().AppendLine().AppendLine("Candidates:");
        var width = Candidates.Count == 0 ? 0 : Candidates.Max(c => c.TargetPath.Length);
        foreach (var candidate in Candidates)
        {
            builder.Append(culture, $"  {candidate.TargetPath.PadRight(width)}  {candidate.Confidence:0.00}");
            if (candidate.ClaimedBySourcePath is { } claimedBy)
            {
                builder.Append(culture, $"  (claimed by {claimedBy})");
            }

            builder.AppendLine();
        }

        builder.AppendLine()
            .Append(culture, $"Selected: {SelectedTargetPath ?? "(none)"}").AppendLine()
            .Append(culture, $"Status: {Status} -> {Outcome}").AppendLine()
            .Append(culture, $"Threshold: {MinimumConfidence:0.00}").AppendLine()
            .Append(culture, $"ConfidenceGap: {(ConfidenceGap is { } gap ? gap.ToString("0.00", culture) : "n/a")}");
        return builder.ToString();
    }
}
