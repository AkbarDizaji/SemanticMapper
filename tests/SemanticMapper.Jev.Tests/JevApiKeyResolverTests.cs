using Microsoft.Extensions.Configuration;

namespace SemanticMapper.Jev.Tests;

public class JevApiKeyResolverTests
{
    private static IConfiguration Config(string? key) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["TypeSafe:ApiKey"] = key })
        .Build();

    private static Func<string, string?> Env(string? key) =>
        name => name == "TYPESAFE_API_KEY" ? key : null;

    [Fact]
    public void Explicit_key_wins_over_configuration_and_environment() =>
        Assert.Equal("explicit", JevApiKeyResolver.Resolve("explicit", Config("config"), Env("env")));

    [Fact]
    public void Configuration_wins_over_environment() =>
        Assert.Equal("config", JevApiKeyResolver.Resolve(null, Config("config"), Env("env")));

    [Fact]
    public void Environment_is_the_last_fallback() =>
        Assert.Equal("env", JevApiKeyResolver.Resolve("  ", Config(""), Env("env")));

    [Fact]
    public void Missing_everywhere_resolves_to_null() =>
        Assert.Null(JevApiKeyResolver.Resolve(null, null, Env(null)));
}
