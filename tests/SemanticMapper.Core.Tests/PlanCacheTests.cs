using SemanticMapper.Core.Tests.Infrastructure;

namespace SemanticMapper.Core.Tests;

public class PlanCacheTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private const string CustomerJson = """
        { "given_name": "Akbar", "surname": "Dizaji", "dob": "1993-06-10", "identity_no": "123456" }
        """;

    [Fact]
    public async Task Second_mapping_with_the_same_schema_uses_the_cached_plan_without_calling_the_matcher()
    {
        var matcher = MappingTests.CustomerMatcher();
        var mapper = MapperFactory.Create(matcher);

        var first = await mapper.MapAsync<Customer>(CustomerJson, Ct);
        var callsAfterFirst = matcher.CallCount;
        var second = await mapper.MapAsync<Customer>(
            """{ "identity_no": "999", "dob": "2000-01-01", "surname": "Other", "given_name": "Someone" }""",
            Ct);

        Assert.False(first.UsedCachedPlan);
        Assert.True(second.UsedCachedPlan);
        Assert.Equal(4, callsAfterFirst);
        Assert.Equal(callsAfterFirst, matcher.CallCount);
        Assert.Equal("Someone", second.Value.FirstName);
        Assert.Equal("999", second.Value.NationalId);
        Assert.Equal(new DateTime(2000, 1, 1), second.Value.BirthDate);
    }

    [Fact]
    public async Task Cached_plan_is_shared_between_json_and_xml_inputs_with_the_same_schema()
    {
        var matcher = MappingTests.CustomerMatcher();
        var mapper = MapperFactory.Create(matcher);

        await mapper.MapAsync<Customer>(CustomerJson, Ct);
        var xml = await mapper.MapAsync<Customer>(
            "<c><given_name>A</given_name><surname>B</surname><dob>1990-01-01</dob><identity_no>1</identity_no></c>",
            Ct);

        Assert.True(xml.UsedCachedPlan);
        Assert.Equal("A", xml.Value.FirstName);
    }

    [Fact]
    public async Task Diagnostics_from_a_cached_plan_contain_the_original_decisions()
    {
        var mapper = MapperFactory.Create(MappingTests.CustomerMatcher());

        var first = await mapper.MapAsync<Customer>(CustomerJson, Ct);
        var second = await mapper.MapAsync<Customer>(CustomerJson, Ct);

        Assert.Equal(first.Diagnostics.PlanKey, second.Diagnostics.PlanKey);
        Assert.Equal(
            first.Diagnostics.Decisions.Select(d => (d.SourcePath, d.SelectedTargetPath, d.SelectedConfidence, d.Candidates.Count)),
            second.Diagnostics.Decisions.Select(d => (d.SourcePath, d.SelectedTargetPath, d.SelectedConfidence, d.Candidates.Count)));
    }

    [Fact]
    public async Task Different_source_schema_misses_the_cache()
    {
        var matcher = MappingTests.CustomerMatcher();
        var mapper = MapperFactory.Create(matcher);

        await mapper.MapAsync<Customer>(CustomerJson, Ct);
        var result = await mapper.MapAsync<Customer>("""{ "given_name": "A", "surname": "B" }""", Ct);

        Assert.False(result.UsedCachedPlan);
        Assert.Equal(6, matcher.CallCount);
    }

    [Fact]
    public async Task Changed_destination_schema_does_not_reuse_the_old_plan()
    {
        var cache = new InMemoryMappingPlanCache();
        var matcher = new FakeSemanticFieldMatcher()
            .Score("given_name", ("FirstName", 0.95))
            .Score("surname", ("LastName", 0.95));
        var mapper = MapperFactory.Create(matcher, cache: cache);
        const string json = """{ "given_name": "A", "surname": "B" }""";

        var v1 = await mapper.MapAsync<V1.Customer>(json, Ct);
        var v2 = await mapper.MapAsync<V2.Customer>(json, Ct);

        Assert.False(v2.UsedCachedPlan);
        Assert.NotEqual(v1.Diagnostics.PlanKey, v2.Diagnostics.PlanKey);
        Assert.Equal("B", v2.Value.LastName);
        Assert.Equal(4, matcher.CallCount);
    }

    [Fact]
    public async Task Changed_matcher_version_does_not_reuse_the_old_plan()
    {
        var cache = new InMemoryMappingPlanCache();
        var matcher = MappingTests.CustomerMatcher();
        await MapperFactory.Create(matcher, cache: cache).MapAsync<Customer>(CustomerJson, Ct);

        matcher.Version = "fake-2";
        var result = await MapperFactory.Create(matcher, cache: cache).MapAsync<Customer>(CustomerJson, Ct);

        Assert.False(result.UsedCachedPlan);
    }

    [Fact]
    public async Task Changed_confidence_policy_does_not_reuse_the_old_plan()
    {
        var cache = new InMemoryMappingPlanCache();
        var matcher = MappingTests.CustomerMatcher();
        await MapperFactory.Create(matcher, o => o.MinimumConfidence = 0.5, cache).MapAsync<Customer>(CustomerJson, Ct);

        var result = await MapperFactory.Create(matcher, o => o.MinimumConfidence = 0.9, cache).MapAsync<Customer>(CustomerJson, Ct);

        Assert.False(result.UsedCachedPlan);
    }

    [Fact]
    public async Task Rejected_mappings_are_not_cached()
    {
        var matcher = new FakeSemanticFieldMatcher().Score("name", ("FirstName", 0.3));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = MappingFailureBehavior.Throw);

        await Assert.ThrowsAsync<UnsafeMappingException>(() => mapper.MapAsync<Customer>("""{ "name": "A" }""", Ct));
        await Assert.ThrowsAsync<UnsafeMappingException>(() => mapper.MapAsync<Customer>("""{ "name": "A" }""", Ct));

        Assert.Equal(2, matcher.CallCount);
    }

    [Fact]
    public async Task Plan_key_is_independent_of_source_field_order_and_values()
    {
        var mapper = MapperFactory.Create(MappingTests.CustomerMatcher());

        var a = await mapper.MapAsync<Customer>("""{ "given_name": "A", "surname": "B", "dob": "1990-01-01", "identity_no": "1" }""", Ct);
        var b = await mapper.MapAsync<Customer>("""{ "identity_no": "2", "dob": null, "surname": "C", "given_name": "D" }""", Ct);

        Assert.Equal(a.Diagnostics.PlanKey, b.Diagnostics.PlanKey);
        Assert.True(b.UsedCachedPlan);
    }

    [Fact]
    public async Task Custom_cache_receives_the_plan_and_serves_it()
    {
        var cache = new RecordingCache();
        var matcher = MappingTests.CustomerMatcher();
        var mapper = MapperFactory.Create(matcher, cache: cache);

        await mapper.MapAsync<Customer>(CustomerJson, Ct);
        await mapper.MapAsync<Customer>(CustomerJson, Ct);

        var plan = Assert.Single(cache.Stored);
        Assert.Equal(typeof(Customer).FullName, plan.DestinationTypeName);
        Assert.Equal("fake-1", plan.MatcherVersion);
        Assert.Equal(4, plan.Decisions.Count);
        Assert.Equal(2, cache.Gets);
    }

    [Fact]
    public async Task InMemory_cache_is_safe_under_concurrent_access()
    {
        using var cache = new InMemoryMappingPlanCache();
        var plans = Enumerable.Range(0, 200).Select(i => Plan($"k{i % 20}")).ToList();

        await Parallel.ForEachAsync(plans, Ct, async (plan, ct) =>
        {
            await cache.SetAsync(plan, ct);
            Assert.NotNull(await cache.GetAsync(plan.Key, ct));
        });

        for (var i = 0; i < 20; i++)
        {
            Assert.NotNull(await cache.GetAsync(new MappingPlanKey($"k{i}"), Ct));
        }
    }

    [Fact]
    public async Task Concurrent_mappings_through_one_mapper_produce_correct_results()
    {
        var mapper = MapperFactory.Create(MappingTests.CustomerMatcher());

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            mapper.MapAsync<Customer>($$"""{ "given_name": "N{{i}}", "surname": "S", "dob": "1990-01-01", "identity_no": "{{i}}" }""", Ct)));

        Assert.All(results, (r, i) => Assert.Equal($"N{i}", r.Value.FirstName));
    }

    [Fact]
    public async Task InMemory_cache_respects_its_size_limit()
    {
        using var cache = new InMemoryMappingPlanCache(Microsoft.Extensions.Options.Options.Create(new InMemoryMappingPlanCacheOptions { SizeLimit = 2 }));

        await cache.SetAsync(Plan("a"), Ct);
        await cache.SetAsync(Plan("b"), Ct);
        await cache.SetAsync(Plan("c"), Ct);

        var present = 0;
        foreach (var key in new[] { "a", "b", "c" })
        {
            present += await cache.GetAsync(new MappingPlanKey(key), Ct) is null ? 0 : 1;
        }

        Assert.True(present <= 2);
    }

    private static MappingPlan Plan(string key) => new()
    {
        Key = new MappingPlanKey(key),
        DestinationTypeName = "T",
        MatcherVersion = "v",
        Decisions = [],
        UnmappedTargetPaths = [],
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private sealed class RecordingCache : IMappingPlanCache
    {
        private readonly Dictionary<MappingPlanKey, MappingPlan> _plans = [];

        public List<MappingPlan> Stored { get; } = [];

        public int Gets { get; private set; }

        public ValueTask<MappingPlan?> GetAsync(MappingPlanKey key, CancellationToken cancellationToken = default)
        {
            Gets++;
            return ValueTask.FromResult(_plans.GetValueOrDefault(key));
        }

        public ValueTask SetAsync(MappingPlan plan, CancellationToken cancellationToken = default)
        {
            Stored.Add(plan);
            _plans[plan.Key] = plan;
            return ValueTask.CompletedTask;
        }
    }
}
