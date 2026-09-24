using SemanticMapper.Core.Tests.Infrastructure;

namespace SemanticMapper.Core.Tests;

public class DiagnosticsTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Decisions_retain_every_candidate_score_in_descending_order()
    {
        var matcher = new FakeSemanticFieldMatcher()
            .Score("dob", ("NationalId", 0.14), ("BirthDate", 0.97), ("FirstName", 0.41));
        var mapper = MapperFactory.Create(matcher);

        var result = await mapper.MapAsync<Customer>("""{ "dob": "1993-06-10" }""", Ct);

        var decision = Assert.Single(result.Diagnostics.Decisions);
        Assert.Equal("dob", decision.SourcePath);
        Assert.Equal("dob", decision.SourceName);
        Assert.Equal(FieldValueKind.Date, decision.SourceValueKind);
        Assert.Equal(
            [("BirthDate", 0.97), ("FirstName", 0.41), ("NationalId", 0.14)],
            decision.Candidates.Select(c => (c.TargetPath, c.Confidence)));
        Assert.Equal("BirthDate", decision.SelectedTargetPath);
        Assert.Equal(0.97, decision.SelectedConfidence);
        Assert.Equal(0.56, decision.ConfidenceGap!.Value, precision: 10);
        Assert.Equal(0.80, decision.MinimumConfidence);
        Assert.Equal(0.10, decision.MinimumConfidenceGap);
        Assert.Equal(DecisionOutcome.Applied, decision.Outcome);
        Assert.Null(decision.AppliedBehavior);
    }

    [Fact]
    public async Task Diagnostics_describe_the_mapping_context()
    {
        var mapper = MapperFactory.Create(new FakeSemanticFieldMatcher().Score("given_name", ("FirstName", 0.9)));

        var result = await mapper.MapAsync<Customer>("""{ "given_name": "A", "orders": [{ "id": 1 }] }""", Ct);

        Assert.Equal(typeof(Customer), result.Diagnostics.DestinationType);
        Assert.Equal(SourceFormat.Json, result.Diagnostics.SourceFormat);
        Assert.Equal("fake-1", result.Diagnostics.MatcherVersion);
        Assert.False(string.IsNullOrEmpty(result.Diagnostics.PlanKey.Value));
        Assert.Equal(["orders"], result.Diagnostics.UnsupportedSourcePaths);
        Assert.Equal(["LastName", "BirthDate", "NationalId"], result.Diagnostics.UnmappedTargetPaths);
    }

    [Fact]
    public async Task Decision_text_rendering_matches_the_documented_shape_and_contains_no_values()
    {
        var matcher = new FakeSemanticFieldMatcher()
            .Score("dob", ("BirthDate", 0.97), ("FirstName", 0.41), ("NationalId", 0.14));
        var mapper = MapperFactory.Create(matcher);

        var result = await mapper.MapAsync<Customer>("""{ "dob": "1993-06-10" }""", Ct);
        var text = result.Diagnostics.Decisions[0].ToString();

        Assert.Contains("Source: dob", text, StringComparison.Ordinal);
        Assert.Contains("BirthDate   0.97", text, StringComparison.Ordinal);
        Assert.Contains("NationalId  0.14", text, StringComparison.Ordinal);
        Assert.Contains("Selected: BirthDate", text, StringComparison.Ordinal);
        Assert.Contains("Threshold: 0.80", text, StringComparison.Ordinal);
        Assert.Contains("ConfidenceGap: 0.56", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1993", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_field_string_representation_excludes_the_value()
    {
        var field = new SourceField("customer.ssn", "ssn", FieldValueKind.String, "123-45-6789");

        Assert.Equal("customer.ssn", field.ToString());
    }
}
