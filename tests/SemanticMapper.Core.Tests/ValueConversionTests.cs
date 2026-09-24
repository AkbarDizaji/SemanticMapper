using SemanticMapper.Core.Tests.Infrastructure;

namespace SemanticMapper.Core.Tests;

public class ValueConversionTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    /// <summary>Maps each source field to the target with the same path, with full confidence.</summary>
    private static async Task<AllTypes> MapAsync(string json)
    {
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var matcher = new FakeSemanticFieldMatcher();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            matcher.Score(property.Name, (property.Name, 1.0));
        }

        var result = await MapperFactory.Create(matcher).MapAsync<AllTypes>(json, Ct);
        return result.Value;
    }

    [Fact]
    public async Task Converts_scalar_values_to_destination_types()
    {
        var value = await MapAsync("""
            {
              "Text": 42,
              "Count": "17",
              "Big": 9007199254740993,
              "Amount": "12.50",
              "Ratio": 0.25,
              "IsActive": "true",
              "When": "2024-01-02T03:04:05",
              "WhenOffset": "2024-01-02T03:04:05+10:00",
              "Day": "1993-06-10",
              "At": "14:30",
              "Duration": "01:30:00",
              "Id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
              "Website": "https://example.com/",
              "Status": "suspended"
            }
            """);

        Assert.Equal("42", value.Text);
        Assert.Equal(17, value.Count);
        Assert.Equal(9007199254740993L, value.Big);
        Assert.Equal(12.50m, value.Amount);
        Assert.Equal(0.25, value.Ratio);
        Assert.True(value.IsActive);
        Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5), value.When);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(10)), value.WhenOffset);
        Assert.Equal(new DateOnly(1993, 6, 10), value.Day);
        Assert.Equal(new TimeOnly(14, 30), value.At);
        Assert.Equal(TimeSpan.FromMinutes(90), value.Duration);
        Assert.Equal(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), value.Id);
        Assert.Equal(new Uri("https://example.com/"), value.Website);
        Assert.Equal(AccountStatus.Suspended, value.Status);
    }

    [Fact]
    public async Task Converts_scalar_arrays_to_collections()
    {
        var value = await MapAsync("""{ "Tags": ["a", "b"], "Scores": [1, "2"], "Ids": ["3f2504e0-4f89-11d3-9a0c-0305e82c3301"] }""");

        Assert.Equal(["a", "b"], value.Tags);
        Assert.Equal([1, 2], value.Scores);
        Assert.Equal([Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301")], value.Ids);
    }

    [Fact]
    public async Task Nullable_properties_accept_values_and_nulls()
    {
        var withValues = await MapAsync("""{ "MaybeCount": 5, "MaybeWhen": "2024-01-02" }""");
        var withNulls = await MapAsync("""{ "MaybeCount": null, "MaybeWhen": null, "Text": null }""");

        Assert.Equal(5, withValues.MaybeCount);
        Assert.Equal(new DateTime(2024, 1, 2), withValues.MaybeWhen);
        Assert.Null(withNulls.MaybeCount);
        Assert.Null(withNulls.MaybeWhen);
        Assert.Null(withNulls.Text);
    }

    [Fact]
    public async Task Null_for_a_non_nullable_value_type_leaves_the_default()
    {
        var value = await MapAsync("""{ "Count": null }""");

        Assert.Equal(0, value.Count);
    }

    [Theory]
    [InlineData("""{ "Count": "abc" }""", "Count")]
    [InlineData("""{ "Count": 1.5 }""", "Count")]
    [InlineData("""{ "Count": 99999999999 }""", "Count")]
    [InlineData("""{ "Day": "not a date" }""", "Day")]
    [InlineData("""{ "Status": "Unknown" }""", "Status")]
    [InlineData("""{ "Status": 42 }""", "Status")]
    [InlineData("""{ "Scores": ["x"] }""", "Scores")]
    public async Task Unconvertible_values_throw_without_exposing_the_value(string json, string target)
    {
        var ex = await Assert.ThrowsAsync<ValueConversionException>(() => MapAsync(json));

        Assert.Equal(target, ex.TargetPath);
        Assert.Equal(target, ex.SourcePath);
        Assert.DoesNotContain("abc", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not a date", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("99999999999", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Xml_values_convert_the_same_way_as_json()
    {
        var matcher = new FakeSemanticFieldMatcher()
            .Score("count", ("Count", 1.0))
            .Score("active", ("IsActive", 1.0))
            .Score("tag", ("Tags", 1.0));
        var mapper = MapperFactory.Create(matcher);

        var result = await mapper.MapAsync<AllTypes>("<r><count>7</count><active>true</active><tag>a</tag><tag>b</tag></r>", Ct);

        Assert.Equal(7, result.Value.Count);
        Assert.True(result.Value.IsActive);
        Assert.Equal(["a", "b"], result.Value.Tags);
    }
}
