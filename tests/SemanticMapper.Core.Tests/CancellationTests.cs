using SemanticMapper.Core.Tests.Infrastructure;

namespace SemanticMapper.Core.Tests;

public class CancellationTests
{
    [Fact]
    public async Task Cancellation_token_is_propagated_to_the_matcher()
    {
        using var cts = new CancellationTokenSource();
        var observedCancellation = false;
        var matcher = MappingTests.CustomerMatcher();
        matcher.OnMatch = async (_, token) =>
        {
            Assert.True(token.CanBeCanceled);
            Assert.False(token.IsCancellationRequested);
            await cts.CancelAsync();
            observedCancellation = token.IsCancellationRequested;
        };
        var mapper = MapperFactory.Create(matcher);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mapper.MapAsync<Customer>("""{ "given_name": "A" }""", cts.Token));

        Assert.True(observedCancellation, "Cancelling the caller's token must cancel the token the matcher received.");
    }

    [Fact]
    public async Task Cancelling_during_matching_stops_the_mapping()
    {
        using var cts = new CancellationTokenSource();
        var matcher = MappingTests.CustomerMatcher();
        matcher.OnMatch = async (_, token) =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
        };
        var mapper = MapperFactory.Create(matcher);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mapper.MapAsync<Customer>("""{ "given_name": "A" }""", cts.Token));
    }

    [Fact]
    public async Task Already_cancelled_token_throws_before_calling_the_matcher()
    {
        var matcher = MappingTests.CustomerMatcher();
        var mapper = MapperFactory.Create(matcher);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mapper.MapAsync<Customer>("""{ "given_name": "A" }""", new CancellationToken(canceled: true)));
        Assert.Equal(0, matcher.CallCount);
    }

    [Fact]
    public async Task Cancellation_token_is_propagated_to_the_plan_cache()
    {
        using var cts = new CancellationTokenSource();
        var cache = new TokenRecordingCache();
        var mapper = MapperFactory.Create(MappingTests.CustomerMatcher(), cache: cache);

        await mapper.MapAsync<Customer>("""{ "given_name": "A" }""", cts.Token);

        Assert.Equal(2, cache.Tokens.Count);
        Assert.All(cache.Tokens, t => Assert.Equal(cts.Token, t));
    }

    private sealed class TokenRecordingCache : IMappingPlanCache
    {
        public List<CancellationToken> Tokens { get; } = [];

        public ValueTask<MappingPlan?> GetAsync(MappingPlanKey key, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return ValueTask.FromResult<MappingPlan?>(null);
        }

        public ValueTask SetAsync(MappingPlan plan, CancellationToken cancellationToken = default)
        {
            Tokens.Add(cancellationToken);
            return ValueTask.CompletedTask;
        }
    }
}
