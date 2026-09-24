namespace SemanticMapper.Jev;

/// <summary>Well-known names used by the Jev provider.</summary>
public static class JevDefaults
{
    /// <summary>
    /// The name of the <see cref="System.Net.Http.HttpClient"/> the provider uses. Configure it with
    /// <c>services.AddHttpClient(JevDefaults.HttpClientName)</c>, for example to add a resilience handler.
    /// </summary>
    public const string HttpClientName = "SemanticMapper.Jev";

    /// <summary>The configuration key checked for the API key when <see cref="JevMatcherOptions.ApiKey"/> is not set.</summary>
    public const string ApiKeyConfigurationKey = "TypeSafe:ApiKey";

    /// <summary>The environment variable checked for the API key after configuration.</summary>
    public const string ApiKeyEnvironmentVariable = "TYPESAFE_API_KEY";
}
