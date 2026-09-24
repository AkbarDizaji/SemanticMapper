using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace SemanticMapper;

/// <summary>
/// A thread-safe, size-bounded, in-process <see cref="IMappingPlanCache"/>.
/// </summary>
/// <remarks>Each instance owns a private cache; no state is shared between instances.</remarks>
public sealed class InMemoryMappingPlanCache : IMappingPlanCache, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly TimeSpan? _slidingExpiration;

    /// <summary>Initializes a new instance with default options.</summary>
    public InMemoryMappingPlanCache()
        : this(Options.Create(new InMemoryMappingPlanCacheOptions()))
    {
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="options">The cache options.</param>
    public InMemoryMappingPlanCache(IOptions<InMemoryMappingPlanCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;
        ArgumentOutOfRangeException.ThrowIfLessThan(value.SizeLimit, 1, nameof(options));
        _slidingExpiration = value.SlidingExpiration;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = value.SizeLimit });
    }

    /// <inheritdoc />
    public ValueTask<MappingPlan?> GetAsync(MappingPlanKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_cache.TryGetValue(key.Value, out MappingPlan? plan) ? plan : null);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(MappingPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        cancellationToken.ThrowIfCancellationRequested();
        var entryOptions = new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = _slidingExpiration };
        _cache.Set(plan.Key.Value, plan, entryOptions);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => _cache.Dispose();
}
