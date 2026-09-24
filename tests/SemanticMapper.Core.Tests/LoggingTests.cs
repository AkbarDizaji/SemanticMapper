using Microsoft.Extensions.Logging;
using SemanticMapper.Core.Tests.Infrastructure;

namespace SemanticMapper.Core.Tests;

public class LoggingTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Field_values_are_never_logged()
    {
        var logger = new CapturingLogger<DefaultSemanticMapper>();
        var matcher = new FakeSemanticFieldMatcher()
            .Score("given_name", ("FirstName", 0.96))
            .Score("dob", ("BirthDate", 0.50))
            .Score("identity_no", ("NationalId", 0.85), ("LastName", 0.84));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = MappingFailureBehavior.UseBestMatch, logger: logger);
        const string json = """{ "given_name": "Akbar", "dob": "1993-06-10", "identity_no": "SECRET-123456", "note": "private-note" }""";

        await mapper.MapAsync<Customer>(json, Ct);
        await mapper.MapAsync<Customer>(json, Ct);

        var text = logger.AllText();
        Assert.NotEmpty(logger.Entries);
        Assert.DoesNotContain("Akbar", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1993-06-10", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-123456", text, StringComparison.Ordinal);
        Assert.DoesNotContain("private-note", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Structured_events_are_logged_for_matching_selection_and_caching()
    {
        var logger = new CapturingLogger<DefaultSemanticMapper>();
        var matcher = new FakeSemanticFieldMatcher()
            .Score("given_name", ("FirstName", 0.96))
            .Score("first", ("FirstName", 0.90))
            .Score("maybe", ("LastName", 0.40))
            .Score("either", ("BirthDate", 0.85), ("NationalId", 0.84));
        var mapper = MapperFactory.Create(matcher, logger: logger);
        const string json = """{ "given_name": "A", "first": "B", "maybe": "C", "either": "D", "unknown": "E" }""";

        await mapper.MapAsync<Customer>(json, Ct);
        await mapper.MapAsync<Customer>(json, Ct);

        Assert.True(logger.Contains("PlanCacheMiss"));
        Assert.True(logger.Contains("PlanCacheHit"));
        Assert.True(logger.Contains("SemanticMatchPerformed"));
        Assert.True(logger.Contains("CandidateSelected"));
        Assert.True(logger.Contains("CandidateRejected"));
        Assert.True(logger.Contains("LowConfidenceMatch"));
        Assert.True(logger.Contains("AmbiguousMatch"));
        Assert.True(logger.Contains("NoSemanticMatch"));

        var selected = logger.Entries.First(e => e.EventId.Name == "CandidateSelected");
        Assert.Contains(selected.State, p => p.Key == "SourcePath" && Equals(p.Value, "given_name"));
        Assert.Contains(selected.State, p => p.Key == "TargetPath" && Equals(p.Value, "FirstName"));
        Assert.Contains(selected.State, p => p.Key == "Confidence");
        Assert.Equal(LogLevel.Warning, logger.Entries.First(e => e.EventId.Name == "LowConfidenceMatch").Level);
    }
}
