using Microsoft.Extensions.DependencyInjection;

namespace SemanticMapper.Jev.Tests;

/// <summary>
/// Calls the real TypeSafe API. Skipped unless TYPESAFE_API_KEY is set, so normal test runs need no key.
/// Run only these with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class JevIntegrationTests
{
    public sealed class Customer
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public DateTime BirthDate { get; set; }
        public string NationalId { get; set; } = "";
    }

    [Fact]
    public async Task Maps_the_customer_example_through_the_real_api()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TYPESAFE_API_KEY")))
        {
            Assert.Skip("Set TYPESAFE_API_KEY to run TypeSafe integration tests.");
        }

        var services = new ServiceCollection();
        services.AddSemanticMapper(o => o.OnLowConfidence = o.OnAmbiguousMatch = MappingFailureBehavior.LeaveDefault).UseJev();
        await using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<ISemanticMapper>();

        var result = await mapper.MapAsync<Customer>(
            """{ "given_name": "Akbar", "surname": "Dizaji", "dob": "1993-06-10", "identity_no": "123456" }""",
            TestContext.Current.CancellationToken);

        foreach (var decision in result.Diagnostics.Decisions)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(decision.ToString());
        }

        Assert.Equal("Akbar", result.Value.FirstName);
        Assert.Equal("Dizaji", result.Value.LastName);
        Assert.Equal(new DateTime(1993, 6, 10), result.Value.BirthDate);
        Assert.Equal("123456", result.Value.NationalId);
    }
}
