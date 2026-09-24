namespace SemanticMapper;

/// <summary>
/// The scores an <see cref="ISemanticFieldMatcher"/> produced for one source field.
/// </summary>
public sealed class FieldMatchResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldMatchResult"/> class.
    /// </summary>
    /// <param name="candidates">
    /// The scored candidates. Candidates that were not scored are treated as confidence 0.
    /// </param>
    /// <param name="noMatchConfidence">
    /// Optional confidence that the source field has no counterpart on the destination type.
    /// When it exceeds the best candidate's confidence the field is treated as unmatched instead of
    /// being subject to the low-confidence policy.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A confidence is outside [0, 1].</exception>
    /// <exception cref="ArgumentException">The same target is scored more than once.</exception>
    public FieldMatchResult(IEnumerable<CandidateScore> candidates, double? noMatchConfidence = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var list = new List<CandidateScore>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate.Target, nameof(candidates));
            EnsureConfidence(candidate.Confidence, nameof(candidates));
            if (!seen.Add(candidate.Target.Path))
            {
                throw new ArgumentException($"Target '{candidate.Target.Path}' was scored more than once.", nameof(candidates));
            }

            list.Add(candidate);
        }

        if (noMatchConfidence is { } noMatch)
        {
            EnsureConfidence(noMatch, nameof(noMatchConfidence));
        }

        // Stable sort keeps the matcher's order for ties.
        Candidates = [.. list.OrderByDescending(c => c.Confidence)];
        NoMatchConfidence = noMatchConfidence;
    }

    /// <summary>Gets a result stating that the source field has no counterpart.</summary>
    public static FieldMatchResult NoMatch { get; } = new([], 1.0);

    /// <summary>Gets the scored candidates ordered by descending confidence.</summary>
    public IReadOnlyList<CandidateScore> Candidates { get; }

    /// <summary>Gets the confidence that the source field has no counterpart, if the matcher reports it.</summary>
    public double? NoMatchConfidence { get; }

    private static void EnsureConfidence(double value, string paramName)
    {
        if (double.IsNaN(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Confidence must be between 0 and 1.");
        }
    }
}
