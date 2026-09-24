namespace SemanticMapper;

/// <summary>
/// Stores mapping plans so documents with a known schema can be mapped without calling the semantic matcher.
/// </summary>
/// <remarks>Implementations must be thread-safe.</remarks>
public interface IMappingPlanCache
{
    /// <summary>Gets a cached plan.</summary>
    /// <param name="key">The plan key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The plan, or <see langword="null"/> if none is cached.</returns>
    ValueTask<MappingPlan?> GetAsync(MappingPlanKey key, CancellationToken cancellationToken = default);

    /// <summary>Stores a plan, replacing any plan with the same key.</summary>
    /// <param name="plan">The plan to store. Its <see cref="MappingPlan.Key"/> is the cache key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    ValueTask SetAsync(MappingPlan plan, CancellationToken cancellationToken = default);
}
