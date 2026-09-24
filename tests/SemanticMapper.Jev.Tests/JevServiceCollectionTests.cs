using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SemanticMapper.Jev.Tests;

public class JevServiceCollectionTests
{
    [Fact]
    public void UseJev_registers_the_jev_matcher()
    {
        var services = new ServiceCollection();
        services.AddSemanticMapper().UseJev(o => o.ApiKey = "k");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<JevSemanticFieldMatcher>(provider.GetRequiredService<ISemanticFieldMatcher>());
        Assert.IsType<DefaultSemanticMapper>(provider.GetRequiredService<ISemanticMapper>());
    }

    [Fact]
    public void Explicit_key_takes_precedence_over_configuration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration("from-config"));
        services.AddSemanticMapper().UseJev(o => o.ApiKey = "explicit");

        using var provider = services.BuildServiceProvider();

        Assert.Equal("explicit", provider.GetRequiredService<IOptions<JevMatcherOptions>>().Value.ApiKey);
    }

    [Fact]
    public void Configuration_key_is_used_when_no_explicit_key_is_set()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration("from-config"));
        services.AddSemanticMapper().UseJev();

        using var provider = services.BuildServiceProvider();

        Assert.Equal("from-config", provider.GetRequiredService<IOptions<JevMatcherOptions>>().Value.ApiKey);
    }

    [Fact]
    public async Task Named_http_client_is_used_and_can_be_customized_by_consumers()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """
            { "answers": { "destination_property": { "probabilities": { "FirstName": 0.97, "LastName": 0.01, "none": 0.02 } } } }
            """);
        var services = new ServiceCollection();
        services.AddSemanticMapper().UseJev(o => o.ApiKey = "k");
        services.AddHttpClient(JevDefaults.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<ISemanticMapper>()
            .MapAsync<Person>("""{ "given_name": "Akbar" }""", TestContext.Current.CancellationToken);

        Assert.Equal("Akbar", result.Value.FirstName);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void Options_ToString_redacts_the_api_key() =>
        Assert.DoesNotContain("secret", new JevMatcherOptions { ApiKey = "secret" }.ToString(), StringComparison.Ordinal);

    private static IConfiguration Configuration(string key) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["TypeSafe:ApiKey"] = key })
        .Build();

    public sealed class Person
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
