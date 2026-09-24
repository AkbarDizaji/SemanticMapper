using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SemanticMapper;
using SemanticMapper.Jev;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the TypeSafe Jev semantic matcher.</summary>
public static class JevSemanticMapperBuilderExtensions
{
    /// <summary>
    /// Uses TypeSafe Jev as the <see cref="ISemanticFieldMatcher"/>, replacing any registered matcher.
    /// </summary>
    /// <remarks>
    /// The API key is resolved in this order: <see cref="JevMatcherOptions.ApiKey"/>, the configuration
    /// key <c>TypeSafe:ApiKey</c> (when an <see cref="IConfiguration"/> is registered), then the
    /// <c>TYPESAFE_API_KEY</c> environment variable. A missing key causes a
    /// <see cref="JevAuthenticationException"/> when the matcher is first used.
    /// </remarks>
    /// <param name="builder">The SemanticMapper builder.</param>
    /// <param name="configure">Configures the provider options.</param>
    /// <returns>The builder.</returns>
    public static ISemanticMapperBuilder UseJev(this ISemanticMapperBuilder builder, Action<JevMatcherOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        services.AddHttpClient(JevDefaults.HttpClientName);

        var options = services.AddOptions<JevMatcherOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options.PostConfigure<IServiceProvider>((o, provider) =>
            o.ApiKey = JevApiKeyResolver.Resolve(o.ApiKey, provider.GetService<IConfiguration>(), Environment.GetEnvironmentVariable));

        services.Replace(ServiceDescriptor.Singleton<ISemanticFieldMatcher>(provider => new JevSemanticFieldMatcher(
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<IOptions<JevMatcherOptions>>(),
            provider.GetService<ILogger<JevSemanticFieldMatcher>>())));

        return builder;
    }
}
