using SemanticMapper.Core.Tests.Infrastructure;

namespace SemanticMapper.Core.Tests;

public class MappingTests
{
    private const string CustomerJson = """
        { "given_name": "Akbar", "surname": "Dizaji", "dob": "1993-06-10", "identity_no": "123456" }
        """;

    internal static FakeSemanticFieldMatcher CustomerMatcher() => new FakeSemanticFieldMatcher()
        .Score("given_name", ("FirstName", 0.96), ("LastName", 0.30))
        .Score("surname", ("LastName", 0.95), ("FirstName", 0.25))
        .Score("dob", ("BirthDate", 0.97), ("NationalId", 0.05))
        .Score("identity_no", ("NationalId", 0.93), ("BirthDate", 0.02));

    [Fact]
    public async Task Maps_the_customer_example_from_json()
    {
        var mapper = MapperFactory.Create(CustomerMatcher(), o => o.OnLowConfidence = o.OnAmbiguousMatch = MappingFailureBehavior.Throw);

        var result = await mapper.MapAsync<Customer>(CustomerJson, TestContext.Current.CancellationToken);

        Assert.Equal("Akbar", result.Value.FirstName);
        Assert.Equal("Dizaji", result.Value.LastName);
        Assert.Equal(new DateTime(1993, 6, 10), result.Value.BirthDate);
        Assert.Equal("123456", result.Value.NationalId);
        Assert.False(result.UsedCachedPlan);
        Assert.All(result.Diagnostics.Decisions, d => Assert.Equal(MatchStatus.Safe, d.Status));
        Assert.Empty(result.Diagnostics.UnmappedTargetPaths);
    }

    [Fact]
    public async Task Maps_the_customer_example_from_xml_with_the_same_matcher()
    {
        var mapper = MapperFactory.Create(CustomerMatcher());

        var result = await mapper.MapAsync<Customer>(
            "<customer><given_name>Akbar</given_name><surname>Dizaji</surname><dob>1993-06-10</dob><identity_no>123456</identity_no></customer>",
            TestContext.Current.CancellationToken);

        Assert.Equal(SourceFormat.Xml, result.Diagnostics.SourceFormat);
        Assert.Equal("Akbar", result.Value.FirstName);
        Assert.Equal("123456", result.Value.NationalId);
        Assert.Equal(new DateTime(1993, 6, 10), result.Value.BirthDate);
    }

    [Fact]
    public async Task Maps_nested_source_fields_into_nested_destination_properties()
    {
        var matcher = new FakeSemanticFieldMatcher()
            .Score("person.given_name", ("FirstName", 0.95))
            .Score("person.home.town", ("Address.City", 0.90), ("Address.Street", 0.20));
        var mapper = MapperFactory.Create(matcher);

        var result = await mapper.MapAsync<CustomerWithAddress>(
            """{ "person": { "given_name": "A", "home": { "town": "Sydney" } } }""",
            TestContext.Current.CancellationToken);

        Assert.Equal("A", result.Value.FirstName);
        Assert.NotNull(result.Value.Address);
        Assert.Equal("Sydney", result.Value.Address.City);
    }

    [Fact]
    public async Task Nested_destination_objects_are_not_created_when_nothing_maps_into_them()
    {
        var matcher = new FakeSemanticFieldMatcher().Score("name", ("FirstName", 0.95));
        var mapper = MapperFactory.Create(matcher);

        var result = await mapper.MapAsync<CustomerWithAddress>("""{ "name": "A" }""", TestContext.Current.CancellationToken);

        Assert.Null(result.Value.Address);
        Assert.Equal(["Address.Street", "Address.City"], result.Diagnostics.UnmappedTargetPaths);
    }

    [Fact]
    public async Task Array_sources_are_only_offered_collection_candidates_and_scalars_only_scalar_candidates()
    {
        var matcher = new FakeSemanticFieldMatcher();
        var mapper = MapperFactory.Create(matcher);

        await mapper.MapAsync<AllTypes>("""{ "labels": ["a"], "name": "x" }""", TestContext.Current.CancellationToken);

        var lists = matcher.Calls.Zip(matcher.CandidateLists).ToDictionary(p => p.First.Path, p => p.Second);
        Assert.All(lists["labels"], t => Assert.True(t.IsCollection));
        Assert.All(lists["name"], t => Assert.False(t.IsCollection));
        Assert.Equal(["Tags", "Scores", "Ids"], lists["labels"].Select(t => t.Path));
    }

    [Fact]
    public async Task Null_input_throws_ArgumentNullException()
    {
        var mapper = MapperFactory.Create(new FakeSemanticFieldMatcher());

        await Assert.ThrowsAsync<ArgumentNullException>(() => mapper.MapAsync<Customer>(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Matcher_returning_unknown_candidates_is_reported_as_matcher_failure()
    {
        var mapper = MapperFactory.Create(new RogueMatcher());

        await Assert.ThrowsAsync<SemanticMatcherException>(() => mapper.MapAsync<Customer>(CustomerJson, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Matcher_exceptions_that_are_not_semantic_mapper_exceptions_are_wrapped()
    {
        var matcher = new FakeSemanticFieldMatcher { OnMatch = (_, _) => throw new InvalidOperationException("boom") };
        var mapper = MapperFactory.Create(matcher);

        var ex = await Assert.ThrowsAsync<SemanticMatcherException>(() => mapper.MapAsync<Customer>(CustomerJson, TestContext.Current.CancellationToken));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    private sealed class RogueMatcher : ISemanticFieldMatcher
    {
        public string Version => "rogue";

        public Task<FieldMatchResult> MatchAsync(SourceField source, IReadOnlyList<TargetField> candidates, CancellationToken cancellationToken = default)
        {
            var foreign = new TargetField("NotAProperty", "NotAProperty", typeof(string), FieldValueKind.String, false, true, "Other");
            return Task.FromResult(new FieldMatchResult([new CandidateScore(foreign, 0.99)]));
        }
    }
}
