namespace SemanticMapper;

/// <summary>
/// A destination candidate and the confidence the matcher gave it for one source field.
/// </summary>
public sealed class CandidateDiagnostic
{
    /// <summary>Gets the destination property path.</summary>
    public required string TargetPath { get; init; }

    /// <summary>Gets the matcher's confidence in [0, 1].</summary>
    public required double Confidence { get; init; }

    /// <summary>Gets the path of another source field that was assigned this candidate, if any.</summary>
    public string? ClaimedBySourcePath { get; init; }

    /// <inheritdoc />
    public override string ToString() => FormattableString.Invariant($"{TargetPath} {Confidence:0.00}");
}
