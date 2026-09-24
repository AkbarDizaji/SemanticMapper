using SemanticMapper.Core.Tests.Infrastructure;

namespace SemanticMapper.Core.Tests;

public class ConfidencePolicyTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(0.80, MatchStatus.Safe)]
    [InlineData(0.79, MatchStatus.LowConfidence)]
    public async Task Confidence_threshold_is_inclusive(double confidence, MatchStatus expected)
    {
        var matcher = new FakeSemanticFieldMatcher().Score("given_name", ("FirstName", confidence));
        var mapper = MapperFactory.Create(matcher, o => o.MinimumConfidence = 0.80);

        var result = await mapper.MapAsync<Customer>("""{ "given_name": "A" }""", Ct);

        var decision = Assert.Single(result.Diagnostics.Decisions);
        Assert.Equal(expected, decision.Status);
        Assert.Equal(0.80, decision.MinimumConfidence);
    }

    [Theory]
    [InlineData(0.85, 0.75, MatchStatus.Safe)]      // gap 0.10
    [InlineData(0.85, 0.76, MatchStatus.Ambiguous)] // gap 0.09
    public async Task Confidence_gap_is_inclusive(double best, double second, MatchStatus expected)
    {
        var matcher = new FakeSemanticFieldMatcher().Score("name", ("FirstName", best), ("LastName", second));
        var mapper = MapperFactory.Create(matcher, o => o.MinimumConfidenceGap = 0.10);

        var result = await mapper.MapAsync<Customer>("""{ "name": "A" }""", Ct);

        var decision = Assert.Single(result.Diagnostics.Decisions);
        Assert.Equal(expected, decision.Status);
        Assert.Equal(best - second, decision.ConfidenceGap!.Value, precision: 10);
    }

    [Fact]
    public async Task Low_confidence_takes_precedence_over_ambiguity()
    {
        var matcher = new FakeSemanticFieldMatcher().Score("name", ("FirstName", 0.50), ("LastName", 0.49));
        var mapper = MapperFactory.Create(matcher);

        var result = await mapper.MapAsync<Customer>("""{ "name": "A" }""", Ct);

        Assert.Equal(MatchStatus.LowConfidence, result.Diagnostics.Decisions[0].Status);
    }

    [Fact]
    public async Task Throw_on_low_confidence_fails_with_diagnostics()
    {
        var matcher = new FakeSemanticFieldMatcher()
            .Score("given_name", ("FirstName", 0.95))
            .Score("maybe", ("LastName", 0.40));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = MappingFailureBehavior.Throw);

        var ex = await Assert.ThrowsAsync<UnsafeMappingException>(() => mapper.MapAsync<Customer>("""{ "given_name": "A", "maybe": "B" }""", Ct));

        var failed = Assert.Single(ex.FailedDecisions);
        Assert.Equal("maybe", failed.SourcePath);
        Assert.Equal(MatchStatus.LowConfidence, failed.Status);
        Assert.Equal(MappingFailureBehavior.Throw, failed.AppliedBehavior);
        Assert.Contains("maybe", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.Diagnostics);
        Assert.Equal(2, ex.Diagnostics.Decisions.Count);
    }

    [Fact]
    public async Task Throw_on_ambiguous_match_fails()
    {
        var matcher = new FakeSemanticFieldMatcher().Score("name", ("FirstName", 0.88), ("LastName", 0.86));
        var mapper = MapperFactory.Create(matcher, o => o.OnAmbiguousMatch = MappingFailureBehavior.Throw);

        var ex = await Assert.ThrowsAsync<UnsafeMappingException>(() => mapper.MapAsync<Customer>("""{ "name": "A" }""", Ct));

        Assert.Equal(MatchStatus.Ambiguous, Assert.Single(ex.FailedDecisions).Status);
    }

    [Fact]
    public async Task Ambiguous_policy_does_not_apply_to_low_confidence_and_vice_versa()
    {
        var matcher = new FakeSemanticFieldMatcher().Score("name", ("FirstName", 0.88), ("LastName", 0.86));
        var mapper = MapperFactory.Create(matcher, o =>
        {
            o.OnLowConfidence = MappingFailureBehavior.Throw;
            o.OnAmbiguousMatch = MappingFailureBehavior.LeaveDefault;
        });

        var result = await mapper.MapAsync<Customer>("""{ "name": "A" }""", Ct);

        Assert.Equal(DecisionOutcome.LeftDefault, result.Diagnostics.Decisions[0].Outcome);
    }

    [Fact]
    public async Task LeaveDefault_keeps_the_property_default_and_reports_it_as_unmapped()
    {
        var matcher = new FakeSemanticFieldMatcher().Score("name", ("FirstName", 0.40));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = MappingFailureBehavior.LeaveDefault);

        var result = await mapper.MapAsync<Customer>("""{ "name": "A" }""", Ct);

        Assert.Equal("", result.Value.FirstName);
        var decision = result.Diagnostics.Decisions[0];
        Assert.Equal(DecisionOutcome.LeftDefault, decision.Outcome);
        Assert.Equal(MappingFailureBehavior.LeaveDefault, decision.AppliedBehavior);
        Assert.Equal("FirstName", decision.SelectedTargetPath);
        Assert.False(decision.IsApplied);
        Assert.Contains("FirstName", result.Diagnostics.UnmappedTargetPaths);
    }

    [Fact]
    public async Task UseBestMatch_applies_the_best_candidate_despite_low_confidence()
    {
        var matcher = new FakeSemanticFieldMatcher().Score("name", ("FirstName", 0.40), ("LastName", 0.10));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = MappingFailureBehavior.UseBestMatch);

        var result = await mapper.MapAsync<Customer>("""{ "name": "A" }""", Ct);

        Assert.Equal("A", result.Value.FirstName);
        var decision = result.Diagnostics.Decisions[0];
        Assert.Equal(MatchStatus.LowConfidence, decision.Status);
        Assert.Equal(DecisionOutcome.Applied, decision.Outcome);
        Assert.Equal(MappingFailureBehavior.UseBestMatch, decision.AppliedBehavior);
    }

    [Fact]
    public async Task UseBestMatch_applies_the_best_candidate_when_ambiguous()
    {
        var matcher = new FakeSemanticFieldMatcher().Score("name", ("LastName", 0.86), ("FirstName", 0.85));
        var mapper = MapperFactory.Create(matcher, o => o.OnAmbiguousMatch = MappingFailureBehavior.UseBestMatch);

        var result = await mapper.MapAsync<Customer>("""{ "name": "A" }""", Ct);

        Assert.Equal("A", result.Value.LastName);
        Assert.Equal("", result.Value.FirstName);
    }

    [Fact]
    public async Task Source_without_candidates_is_unmapped_and_never_fails_the_mapping()
    {
        var matcher = new FakeSemanticFieldMatcher().Score("given_name", ("FirstName", 0.95));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = o.OnAmbiguousMatch = MappingFailureBehavior.Throw);

        var result = await mapper.MapAsync<Customer>("""{ "given_name": "A", "internal_id": 42 }""", Ct);

        var decision = result.Diagnostics.GetDecision("internal_id")!;
        Assert.Equal(MatchStatus.NoMatch, decision.Status);
        Assert.Equal(DecisionOutcome.Unmapped, decision.Outcome);
        Assert.Null(decision.SelectedTargetPath);
        Assert.Empty(decision.Candidates);
    }

    [Fact]
    public async Task Source_the_matcher_considers_unmatched_is_not_subject_to_the_low_confidence_policy()
    {
        var matcher = new FakeSemanticFieldMatcher().ScoreWithNoMatch("created_by", 0.70, ("FirstName", 0.20));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = MappingFailureBehavior.Throw);

        var result = await mapper.MapAsync<Customer>("""{ "created_by": "system" }""", Ct);

        var decision = result.Diagnostics.Decisions[0];
        Assert.Equal(MatchStatus.NoMatch, decision.Status);
        Assert.Equal(0.70, decision.NoMatchConfidence);
        Assert.Single(decision.Candidates);
    }

    [Fact]
    public async Task Candidates_scored_zero_count_as_no_match()
    {
        var matcher = new FakeSemanticFieldMatcher().Score("x", ("FirstName", 0.0));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = MappingFailureBehavior.Throw);

        var result = await mapper.MapAsync<Customer>("""{ "x": "1" }""", Ct);

        Assert.Equal(MatchStatus.NoMatch, result.Diagnostics.Decisions[0].Status);
    }

    [Fact]
    public async Task Throw_reports_every_failed_decision_at_once()
    {
        var matcher = new FakeSemanticFieldMatcher()
            .Score("a", ("FirstName", 0.30))
            .Score("b", ("LastName", 0.85), ("NationalId", 0.84));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = o.OnAmbiguousMatch = MappingFailureBehavior.Throw);

        var ex = await Assert.ThrowsAsync<UnsafeMappingException>(() => mapper.MapAsync<Customer>("""{ "a": "1", "b": "2" }""", Ct));

        Assert.Equal(["a", "b"], ex.FailedDecisions.Select(d => d.SourcePath));
    }
}
