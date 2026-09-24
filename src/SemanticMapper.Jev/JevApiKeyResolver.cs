using Microsoft.Extensions.Configuration;

namespace SemanticMapper.Jev;

/// <summary>Resolves the API key with the precedence explicit option &gt; configuration &gt; environment variable.</summary>
internal static class JevApiKeyResolver
{
    public static string? Resolve(string? explicitKey, IConfiguration? configuration, Func<string, string?> getEnvironmentVariable)
    {
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return explicitKey;
        }

        var configured = configuration?[JevDefaults.ApiKeyConfigurationKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var environment = getEnvironmentVariable(JevDefaults.ApiKeyEnvironmentVariable);
        return string.IsNullOrWhiteSpace(environment) ? null : environment;
    }
}
