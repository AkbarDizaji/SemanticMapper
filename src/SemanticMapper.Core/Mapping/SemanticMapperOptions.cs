namespace SemanticMapper;

/// <summary>
/// Options that control how semantic matches are accepted.
/// </summary>
/// <remarks>
/// A match is safe when its confidence is at least <see cref="MinimumConfidence"/> and it beats the
/// next-best available candidate by at least <see cref="MinimumConfidenceGap"/>. A match that fails the
/// confidence threshold is handled by <see cref="OnLowConfidence"/>. A match that passes the threshold
/// but fails the gap is handled by <see cref="OnAmbiguousMatch"/>.
/// </remarks>
public sealed class SemanticMapperOptions
{
    /// <summary>Gets or sets the minimum confidence, in [0, 1], for a match to be safe. Default 0.80.</summary>
    public double MinimumConfidence { get; set; } = 0.80;

    /// <summary>
    /// Gets or sets the minimum difference, in [0, 1], between the selected candidate and the next-best
    /// available candidate for a match to be safe. Default 0.10.
    /// </summary>
    public double MinimumConfidenceGap { get; set; } = 0.10;

    /// <summary>Gets or sets the behavior when the best match is below <see cref="MinimumConfidence"/>. Default <see cref="MappingFailureBehavior.LeaveDefault"/>.</summary>
    public MappingFailureBehavior OnLowConfidence { get; set; } = MappingFailureBehavior.LeaveDefault;

    /// <summary>Gets or sets the behavior when the best match does not beat the runner-up by <see cref="MinimumConfidenceGap"/>. Default <see cref="MappingFailureBehavior.LeaveDefault"/>.</summary>
    public MappingFailureBehavior OnAmbiguousMatch { get; set; } = MappingFailureBehavior.LeaveDefault;

    /// <summary>Gets or sets the maximum number of concurrent matcher calls when building a plan. Default 4.</summary>
    public int MaxConcurrentMatches { get; set; } = 4;

    /// <summary>Gets or sets the maximum nesting depth of source documents and destination types. Default 32.</summary>
    public int MaxDepth { get; set; } = 32;
}
