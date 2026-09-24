namespace SemanticMapper;

/// <summary>Options for <see cref="InMemoryMappingPlanCache"/>.</summary>
public sealed class InMemoryMappingPlanCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum number of plans to keep. Default 1024. When the limit is reached,
    /// entries are compacted and new plans may not be stored until space is available.
    /// </summary>
    public int SizeLimit { get; set; } = 1024;

    /// <summary>Gets or sets how long an unused plan is kept. <see langword="null"/> (the default) keeps plans until evicted by size.</summary>
    public TimeSpan? SlidingExpiration { get; set; }
}
