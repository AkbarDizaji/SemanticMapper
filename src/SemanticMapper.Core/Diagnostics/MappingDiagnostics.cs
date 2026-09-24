namespace SemanticMapper;

/// <summary>
/// Diagnostics for a mapping: every source-field decision with all candidate scores.
/// </summary>
/// <remarks>Diagnostics contain field paths and scores but never source values.</remarks>
public sealed class MappingDiagnostics
{
    /// <summary>Gets the detected format of the source document.</summary>
    public required SourceFormat SourceFormat { get; init; }

    /// <summary>Gets the destination type.</summary>
    public required Type DestinationType { get; init; }

    /// <summary>Gets the mapping-plan cache key used for this mapping.</summary>
    public required MappingPlanKey PlanKey { get; init; }

    /// <summary>Gets the <see cref="ISemanticFieldMatcher.Version"/> that produced the plan.</summary>
    public required string MatcherVersion { get; init; }

    /// <summary>Gets one decision per source field, in document order.</summary>
    public required IReadOnlyList<FieldMappingDecision> Decisions { get; init; }

    /// <summary>Gets the destination property paths that received no value.</summary>
    public required IReadOnlyList<string> UnmappedTargetPaths { get; init; }

    /// <summary>Gets source paths that were skipped because their shape is not supported (for example arrays of objects).</summary>
    public required IReadOnlyList<string> UnsupportedSourcePaths { get; init; }

    /// <summary>Gets the decisions whose values were written to the destination.</summary>
    public IEnumerable<FieldMappingDecision> AppliedDecisions => Decisions.Where(d => d.IsApplied);

    /// <summary>Gets the decision for a source path, or <see langword="null"/> if there is none.</summary>
    /// <param name="sourcePath">The source field path.</param>
    public FieldMappingDecision? GetDecision(string sourcePath) =>
        Decisions.FirstOrDefault(d => string.Equals(d.SourcePath, sourcePath, StringComparison.Ordinal));
}
