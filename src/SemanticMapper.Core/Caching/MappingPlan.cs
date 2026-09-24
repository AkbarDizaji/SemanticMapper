namespace SemanticMapper;

/// <summary>
/// A reusable mapping from a source schema to a destination type, created after a successful mapping.
/// </summary>
/// <remarks>
/// Plans are plain data so cache implementations can serialize them. They hold paths and scores
/// but never source values.
/// </remarks>
public sealed class MappingPlan
{
    /// <summary>Gets the key the plan was created for.</summary>
    public required MappingPlanKey Key { get; init; }

    /// <summary>Gets the full name of the destination type.</summary>
    public required string DestinationTypeName { get; init; }

    /// <summary>Gets the matcher version that produced the scores.</summary>
    public required string MatcherVersion { get; init; }

    /// <summary>Gets the decision for every source field, in document order.</summary>
    public required IReadOnlyList<FieldMappingDecision> Decisions { get; init; }

    /// <summary>Gets the destination property paths that receive no value under this plan.</summary>
    public required IReadOnlyList<string> UnmappedTargetPaths { get; init; }

    /// <summary>Gets when the plan was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
