using SemanticMapper.Internal.Schema;

namespace SemanticMapper.Core.Tests;

public class TargetSchemaTests
{
    [Fact]
    public void Extracts_settable_properties_in_declaration_order()
    {
        var schema = TargetSchemaBuilder.Build(typeof(Customer), maxDepth: 32);

        Assert.Equal(["FirstName", "LastName", "BirthDate", "NationalId"], schema.Fields.Select(f => f.Path));
        var birthDate = schema.Fields[2];
        Assert.Equal(FieldValueKind.DateTime, birthDate.ValueKind);
        Assert.Equal(typeof(DateTime), birthDate.PropertyType);
        Assert.False(birthDate.IsNullable);
        Assert.Equal("Customer", birthDate.DeclaringTypeName);
    }

    [Fact]
    public void Nested_complex_properties_are_flattened_into_paths()
    {
        var schema = TargetSchemaBuilder.Build(typeof(CustomerWithAddress), maxDepth: 32);

        Assert.Equal(["FirstName", "Address.Street", "Address.City"], schema.Fields.Select(f => f.Path));
        Assert.Equal("City", schema.Fields[2].Name);
        Assert.Equal("Address", schema.Fields[2].DeclaringTypeName);
    }

    [Fact]
    public void Value_kinds_collections_and_nullability_are_detected()
    {
        var fields = TargetSchemaBuilder.Build(typeof(AllTypes), maxDepth: 32).Fields.ToDictionary(f => f.Path);

        Assert.Equal(FieldValueKind.Integer, fields["Count"].ValueKind);
        Assert.Equal(FieldValueKind.Decimal, fields["Amount"].ValueKind);
        Assert.Equal(FieldValueKind.Date, fields["Day"].ValueKind);
        Assert.Equal(FieldValueKind.Time, fields["At"].ValueKind);
        Assert.Equal(FieldValueKind.Duration, fields["Duration"].ValueKind);
        Assert.Equal(FieldValueKind.Guid, fields["Id"].ValueKind);
        Assert.Equal(FieldValueKind.Enumeration, fields["Status"].ValueKind);

        Assert.True(fields["MaybeCount"].IsNullable);
        Assert.Equal(FieldValueKind.Integer, fields["MaybeCount"].ValueKind);
        Assert.True(fields["Text"].IsNullable);
        Assert.False(fields["Count"].IsNullable);

        Assert.True(fields["Tags"].IsCollection);
        Assert.Equal(FieldValueKind.String, fields["Tags"].ValueKind);
        Assert.False(fields["Tags"].IsNullable);
        Assert.True(fields["Scores"].IsCollection);
        Assert.True(fields["Ids"].IsCollection);
        Assert.True(fields["Ids"].IsNullable);
    }

    [Fact]
    public void Get_only_and_private_setter_properties_are_skipped_but_init_only_is_included()
    {
        var schema = TargetSchemaBuilder.Build(typeof(WithUnsettable), maxDepth: 32);

        Assert.Equal(["Settable", "InitOnly"], schema.Fields.Select(f => f.Path));
    }

    [Fact]
    public void Self_referencing_types_do_not_recurse_forever()
    {
        var schema = TargetSchemaBuilder.Build(typeof(Node), maxDepth: 32);

        Assert.Equal(["Name"], schema.Fields.Select(f => f.Path));
    }

    [Theory]
    [InlineData(typeof(AbstractModel))]
    [InlineData(typeof(NoDefaultConstructor))]
    [InlineData(typeof(NoProperties))]
    [InlineData(typeof(string))]
    [InlineData(typeof(IDisposable))]
    public void Unsupported_destination_types_throw(Type type)
    {
        var ex = Assert.Throws<UnsupportedDestinationTypeException>(() => TargetSchemaBuilder.Build(type, maxDepth: 32));
        Assert.Equal(type, ex.DestinationType);
    }

    [Fact]
    public void Fingerprint_is_stable_and_changes_with_the_schema()
    {
        var a = TargetSchemaBuilder.Build(typeof(V1.Customer), 32).Fingerprint;
        var b = TargetSchemaBuilder.Build(typeof(V1.Customer), 32).Fingerprint;
        var c = TargetSchemaBuilder.Build(typeof(V2.Customer), 32).Fingerprint;

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Fingerprint_depends_on_fields_not_only_the_type_name()
    {
        // Simulates a destination type that keeps its name but changes shape between deployments.
        var before = TargetSchemaBuilder.ComputeFingerprint("App.Customer", [Field("FirstName", typeof(string))]);
        var after = TargetSchemaBuilder.ComputeFingerprint("App.Customer", [Field("FirstName", typeof(string)), Field("LastName", typeof(string))]);
        var retyped = TargetSchemaBuilder.ComputeFingerprint("App.Customer", [Field("FirstName", typeof(int))]);

        Assert.NotEqual(before, after);
        Assert.NotEqual(before, retyped);
    }

    private static TargetField Field(string path, Type type) =>
        new(path, path, type, FieldValueKind.String, isCollection: false, isNullable: true, "Customer");
}
