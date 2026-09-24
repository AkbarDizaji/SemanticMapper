using SemanticMapper.Internal.Parsing;

namespace SemanticMapper.Core.Tests;

public class DocumentParserTests
{
    private const int MaxDepth = 32;

    [Fact]
    public void Json_flat_object_produces_fields_with_inferred_kinds()
    {
        var doc = DocumentParser.Parse(
            """{ "given_name": "Akbar", "dob": "1993-06-10", "age": 33, "score": 4.5, "vip": true, "note": null, "id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301", "at": "2024-01-02T03:04:05Z" }""",
            MaxDepth);

        Assert.Equal(SourceFormat.Json, doc.Format);
        var byPath = doc.Fields.ToDictionary(f => f.Path);
        Assert.Equal(FieldValueKind.String, byPath["given_name"].ValueKind);
        Assert.Equal("Akbar", byPath["given_name"].Value);
        Assert.Equal(FieldValueKind.Date, byPath["dob"].ValueKind);
        Assert.Equal(FieldValueKind.Integer, byPath["age"].ValueKind);
        Assert.Equal("33", byPath["age"].Value);
        Assert.Equal(FieldValueKind.Decimal, byPath["score"].ValueKind);
        Assert.Equal(FieldValueKind.Boolean, byPath["vip"].ValueKind);
        Assert.Equal("true", byPath["vip"].Value);
        Assert.Equal(FieldValueKind.Null, byPath["note"].ValueKind);
        Assert.Null(byPath["note"].Value);
        Assert.Equal(FieldValueKind.Guid, byPath["id"].ValueKind);
        Assert.Equal(FieldValueKind.DateTime, byPath["at"].ValueKind);
    }

    [Fact]
    public void Json_preserves_document_order()
    {
        var doc = DocumentParser.Parse("""{ "b": 1, "a": 2, "c": 3 }""", MaxDepth);

        Assert.Equal(["b", "a", "c"], doc.Fields.Select(f => f.Path));
    }

    [Fact]
    public void Json_nested_objects_are_flattened_into_dotted_paths()
    {
        var doc = DocumentParser.Parse("""{ "customer": { "name": "A", "address": { "city": "Sydney" } } }""", MaxDepth);

        Assert.Equal(["customer.name", "customer.address.city"], doc.Fields.Select(f => f.Path));
        Assert.Equal("city", doc.Fields[1].Name);
    }

    [Fact]
    public void Json_segment_names_containing_dots_are_quoted_to_keep_paths_unique()
    {
        var doc = DocumentParser.Parse("""{ "a.b": 1, "a": { "b": 2 } }""", MaxDepth);

        Assert.Equal(["['a.b']", "a.b"], doc.Fields.Select(f => f.Path));
        Assert.Equal("a.b", doc.Fields[0].Name);
    }

    [Fact]
    public void Json_scalar_arrays_become_a_single_array_field()
    {
        var doc = DocumentParser.Parse("""{ "tags": ["a", "b"], "scores": [1, 2, 3], "empty": [] }""", MaxDepth);

        var tags = doc.Fields.Single(f => f.Path == "tags");
        Assert.True(tags.IsArray);
        Assert.Equal(FieldValueKind.String, tags.ValueKind);
        Assert.Equal(["a", "b"], tags.Items);
        Assert.Null(tags.Value);
        Assert.Equal(FieldValueKind.Integer, doc.Fields.Single(f => f.Path == "scores").ValueKind);
        Assert.Equal(FieldValueKind.Unknown, doc.Fields.Single(f => f.Path == "empty").ValueKind);
    }

    [Fact]
    public void Json_arrays_of_objects_are_reported_as_unsupported()
    {
        var doc = DocumentParser.Parse("""{ "name": "x", "orders": [{ "id": 1 }] }""", MaxDepth);

        Assert.Equal(["name"], doc.Fields.Select(f => f.Path));
        Assert.Equal(["orders"], doc.UnsupportedPaths);
    }

    [Fact]
    public void Json_duplicate_paths_are_rejected()
    {
        Assert.Throws<InvalidSourceDocumentException>(() => DocumentParser.Parse("""{ "a": 1, "a": 2 }""", MaxDepth));
    }

    [Theory]
    [InlineData("""[{ "a": 1 }]""")]
    [InlineData("""{ "a": """)]
    [InlineData("hello world")]
    [InlineData("   ")]
    [InlineData("")]
    public void Invalid_or_unsupported_input_throws_InvalidSourceDocumentException(string input)
    {
        Assert.Throws<InvalidSourceDocumentException>(() => DocumentParser.Parse(input, MaxDepth));
    }

    [Fact]
    public void Json_deeper_than_max_depth_is_rejected()
    {
        Assert.Throws<InvalidSourceDocumentException>(() => DocumentParser.Parse("""{ "a": { "b": { "c": 1 } } }""", maxDepth: 2));
    }

    [Fact]
    public void Leading_whitespace_and_byte_order_mark_are_ignored_during_detection()
    {
        Assert.Equal(SourceFormat.Json, DocumentParser.Parse("﻿ \n {\"a\":1}", MaxDepth).Format);
        Assert.Equal(SourceFormat.Xml, DocumentParser.Parse("﻿ \n <r><a>1</a></r>", MaxDepth).Format);
    }

    [Fact]
    public void Xml_root_element_is_the_document_root_and_children_become_fields()
    {
        var doc = DocumentParser.Parse(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <customer>
              <given_name>Akbar</given_name>
              <dob>1993-06-10</dob>
              <age>33</age>
            </customer>
            """,
            MaxDepth);

        Assert.Equal(SourceFormat.Xml, doc.Format);
        Assert.Equal(["given_name", "dob", "age"], doc.Fields.Select(f => f.Path));
        Assert.Equal(FieldValueKind.Date, doc.Fields[1].ValueKind);
        Assert.Equal(FieldValueKind.Integer, doc.Fields[2].ValueKind);
        Assert.Equal("33", doc.Fields[2].Value);
    }

    [Fact]
    public void Xml_nested_elements_and_attributes_are_flattened()
    {
        var doc = DocumentParser.Parse(
            """<order id="7"><customer><address><city>Sydney</city></address></customer><total currency="AUD">10.50</total></order>""",
            MaxDepth);

        Assert.Equal(["@id", "customer.address.city", "total", "total.@currency"], doc.Fields.Select(f => f.Path));
        Assert.Equal("id", doc.Fields[0].Name);
        Assert.Equal(FieldValueKind.Decimal, doc.Fields[2].ValueKind);
    }

    [Fact]
    public void Xml_repeated_simple_elements_become_an_array_field()
    {
        var doc = DocumentParser.Parse("<r><tag>a</tag><tag>b</tag><item><x>1</x></item><item><x>2</x></item></r>", MaxDepth);

        var tag = Assert.Single(doc.Fields);
        Assert.True(tag.IsArray);
        Assert.Equal(["a", "b"], tag.Items);
        Assert.Equal(["item"], doc.UnsupportedPaths);
    }

    [Fact]
    public void Xml_empty_and_nil_elements_are_null()
    {
        var doc = DocumentParser.Parse(
            """<r xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><a/><b xsi:nil="true"></b></r>""",
            MaxDepth);

        Assert.All(doc.Fields, f => Assert.Equal(FieldValueKind.Null, f.ValueKind));
        Assert.Equal(["a", "b"], doc.Fields.Select(f => f.Path));
    }

    [Fact]
    public void Xml_with_dtd_is_rejected_to_prevent_entity_expansion_attacks()
    {
        const string xxe = """
            <?xml version="1.0"?>
            <!DOCTYPE r [ <!ENTITY xxe SYSTEM "file:///etc/passwd"> ]>
            <r><a>&xxe;</a></r>
            """;

        Assert.Throws<InvalidSourceDocumentException>(() => DocumentParser.Parse(xxe, MaxDepth));
    }

    [Fact]
    public void Malformed_xml_is_rejected()
    {
        Assert.Throws<InvalidSourceDocumentException>(() => DocumentParser.Parse("<r><a></r>", MaxDepth));
    }

    [Fact]
    public void Equivalent_json_and_xml_produce_the_same_normalized_paths()
    {
        var json = DocumentParser.Parse("""{ "given_name": "A", "address": { "city": "S" }, "tags": ["x", "y"] }""", MaxDepth);
        var xml = DocumentParser.Parse("<c><given_name>A</given_name><address><city>S</city></address><tags>x</tags><tags>y</tags></c>", MaxDepth);

        Assert.Equal(json.Fields.Select(f => (f.Path, f.IsArray)), xml.Fields.Select(f => (f.Path, f.IsArray)));
    }
}
