using SemanticMapper.Core.Tests.Infrastructure;

namespace SemanticMapper.Core.Tests;

public class AssignmentTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_sources_competing_for_one_target_resolve_to_the_higher_score()
    {
        var matcher = new FakeSemanticFieldMatcher()
            .Score("first_name", ("FirstName", 0.90))
            .Score("given_name", ("FirstName", 0.97));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = o.OnAmbiguousMatch = MappingFailureBehavior.Throw);

        var result = await mapper.MapAsync<Customer>("""{ "first_name": "Loser", "given_name": "Winner" }""", Ct);

        Assert.Equal("Winner", result.Value.FirstName);
        var loser = result.Diagnostics.GetDecision("first_name")!;
        Assert.Equal(MatchStatus.Conflict, loser.Status);
        Assert.Equal(DecisionOutcome.Unmapped, loser.Outcome);
        Assert.Equal("given_name", loser.Candidates.Single().ClaimedBySourcePath);
    }

    [Fact]
    public async Task A_source_that_loses_its_best_target_falls_back_to_its_next_candidate_under_policy()
    {
        var matcher = new FakeSemanticFieldMatcher()
            .Score("given_name", ("FirstName", 0.97), ("LastName", 0.10))
            .Score("full_name", ("FirstName", 0.70), ("LastName", 0.50));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = MappingFailureBehavior.LeaveDefault);

        var result = await mapper.MapAsync<Customer>("""{ "given_name": "A", "full_name": "A B" }""", Ct);

        Assert.Equal("A", result.Value.FirstName);
        Assert.Equal("", result.Value.LastName);
        var fullName = result.Diagnostics.GetDecision("full_name")!;
        Assert.Equal("LastName", fullName.SelectedTargetPath);
        Assert.Equal(MatchStatus.LowConfidence, fullName.Status);
        Assert.Equal(DecisionOutcome.LeftDefault, fullName.Outcome);
    }

    [Fact]
    public async Task Candidates_claimed_by_other_sources_do_not_count_against_the_confidence_gap()
    {
        // In isolation "surname" is ambiguous (0.90 vs 0.85), but FirstName is confidently claimed by
        // "given_name", so across the whole matrix LastName is the only real option.
        var matcher = new FakeSemanticFieldMatcher()
            .Score("given_name", ("FirstName", 0.98))
            .Score("surname", ("LastName", 0.90), ("FirstName", 0.85));
        var mapper = MapperFactory.Create(matcher, o => o.OnAmbiguousMatch = MappingFailureBehavior.Throw);

        var result = await mapper.MapAsync<Customer>("""{ "given_name": "A", "surname": "B" }""", Ct);

        var surname = result.Diagnostics.GetDecision("surname")!;
        Assert.Equal(MatchStatus.Safe, surname.Status);
        Assert.Equal(0.90, surname.ConfidenceGap!.Value, precision: 10);
        Assert.Equal("B", result.Value.LastName);
    }

    [Fact]
    public async Task Each_source_is_assigned_to_at_most_one_target_and_each_target_to_at_most_one_source()
    {
        var matcher = new FakeSemanticFieldMatcher()
            .Score("name", ("FirstName", 0.95), ("LastName", 0.94))
            .Score("other", ("FirstName", 0.93), ("LastName", 0.92));
        var mapper = MapperFactory.Create(matcher, o => o.OnLowConfidence = o.OnAmbiguousMatch = MappingFailureBehavior.UseBestMatch);

        var result = await mapper.MapAsync<Customer>("""{ "name": "A", "other": "B" }""", Ct);

        var applied = result.Diagnostics.AppliedDecisions.ToList();
        Assert.Equal(applied.Count, applied.Select(d => d.SelectedTargetPath).Distinct().Count());
        Assert.Equal(("A", "B"), (result.Value.FirstName, result.Value.LastName));
    }

    [Fact]
    public async Task Targets_released_by_LeaveDefault_become_available_to_other_sources()
    {
        // "name" is ambiguous and is left default, which frees FirstName for "given".
        var matcher = new FakeSemanticFieldMatcher()
            .Score("name", ("FirstName", 0.90), ("NationalId", 0.89))
            .Score("given", ("FirstName", 0.85));
        var mapper = MapperFactory.Create(matcher, o => o.OnAmbiguousMatch = MappingFailureBehavior.LeaveDefault);

        var result = await mapper.MapAsync<Customer>("""{ "name": "X", "given": "Y" }""", Ct);

        Assert.Equal("Y", result.Value.FirstName);
        Assert.Equal(DecisionOutcome.LeftDefault, result.Diagnostics.GetDecision("name")!.Outcome);
        Assert.Equal(MatchStatus.Safe, result.Diagnostics.GetDecision("given")!.Status);
    }

    [Fact]
    public async Task Ties_are_resolved_deterministically_by_document_order()
    {
        var matcher = new FakeSemanticFieldMatcher()
            .Score("a", ("FirstName", 0.90))
            .Score("b", ("FirstName", 0.90));
        var mapper = MapperFactory.Create(matcher);

        var result = await mapper.MapAsync<Customer>("""{ "a": "first", "b": "second" }""", Ct);

        Assert.Equal("first", result.Value.FirstName);
    }
}
