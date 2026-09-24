using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SemanticMapper.Core.Tests.Infrastructure;

namespace SemanticMapper.Core.Tests;

public class ServiceCollectionTests
{
    [Fact]
    public void AddSemanticMapper_registers_mapper_cache_and_options()
    {
        var services = new ServiceCollection();
        services.AddSemanticMapper(o =>
        {
            o.MinimumConfidence = 0.9;
            o.MinimumConfidenceGap = 0.2;
            o.OnLowConfidence = MappingFailureBehavior.Throw;
            o.OnAmbiguousMatch = MappingFailureBehavior.UseBestMatch;
        }).UseMatcher<FakeSemanticFieldMatcher>();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.IsType<DefaultSemanticMapper>(provider.GetRequiredService<ISemanticMapper>());
        Assert.IsType<InMemoryMappingPlanCache>(provider.GetRequiredService<IMappingPlanCache>());
        var options = provider.GetRequiredService<IOptions<SemanticMapperOptions>>().Value;
        Assert.Equal(0.9, options.MinimumConfidence);
        Assert.Equal(0.2, options.MinimumConfidenceGap);
        Assert.Equal(MappingFailureBehavior.Throw, options.OnLowConfidence);
        Assert.Equal(MappingFailureBehavior.UseBestMatch, options.OnAmbiguousMatch);
    }

    [Fact]
    public void Custom_cache_replaces_the_in_memory_cache()
    {
        var services = new ServiceCollection();
        services.AddSemanticMapper().UseMatcher<FakeSemanticFieldMatcher>().UsePlanCache<NullCache>();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<NullCache>(provider.GetRequiredService<IMappingPlanCache>());
    }

    [Theory]
    [InlineData(1.5, 0.1, 4)]
    [InlineData(0.8, -0.1, 4)]
    [InlineData(0.8, 0.1, 0)]
    public void Invalid_options_are_rejected(double minimumConfidence, double gap, int concurrency)
    {
        var services = new ServiceCollection();
        services.AddSemanticMapper(o =>
        {
            o.MinimumConfidence = minimumConfidence;
            o.MinimumConfidenceGap = gap;
            o.MaxConcurrentMatches = concurrency;
        }).UseMatcher<FakeSemanticFieldMatcher>();

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<ISemanticMapper>());
    }

    private sealed class NullCache : IMappingPlanCache
    {
        public ValueTask<MappingPlan?> GetAsync(MappingPlanKey key, CancellationToken cancellationToken = default) => ValueTask.FromResult<MappingPlan?>(null);

        public ValueTask SetAsync(MappingPlan plan, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
