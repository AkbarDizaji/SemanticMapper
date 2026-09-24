using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SemanticMapper;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers SemanticMapper services.</summary>
public static class SemanticMapperServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISemanticMapper"/> and an in-memory <see cref="IMappingPlanCache"/>.
    /// A semantic matcher must also be registered, for example with <c>UseJev()</c> or
    /// <see cref="UseMatcher{TMatcher}(ISemanticMapperBuilder)"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the mapper options.</param>
    /// <returns>A builder for further configuration.</returns>
    public static ISemanticMapperBuilder AddSemanticMapper(
        this IServiceCollection services,
        Action<SemanticMapperOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<SemanticMapperOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SemanticMapperOptions>, SemanticMapperOptionsValidator>());
        services.AddOptions<InMemoryMappingPlanCacheOptions>();
        services.TryAddSingleton<IMappingPlanCache, InMemoryMappingPlanCache>();
        services.TryAddSingleton<ISemanticMapper, DefaultSemanticMapper>();
        return new SemanticMapperBuilder(services);
    }

    /// <summary>Uses a custom <see cref="ISemanticFieldMatcher"/>, replacing any registered matcher.</summary>
    /// <typeparam name="TMatcher">The matcher type. It is registered as a singleton.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    public static ISemanticMapperBuilder UseMatcher<TMatcher>(this ISemanticMapperBuilder builder)
        where TMatcher : class, ISemanticFieldMatcher
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<ISemanticFieldMatcher, TMatcher>());
        return builder;
    }

    /// <summary>Uses a custom <see cref="IMappingPlanCache"/>, replacing the in-memory cache.</summary>
    /// <typeparam name="TCache">The cache type. It is registered as a singleton.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    public static ISemanticMapperBuilder UsePlanCache<TCache>(this ISemanticMapperBuilder builder)
        where TCache : class, IMappingPlanCache
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<IMappingPlanCache, TCache>());
        return builder;
    }

    private sealed class SemanticMapperBuilder(IServiceCollection services) : ISemanticMapperBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
