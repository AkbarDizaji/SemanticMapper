using System.Text.Json;

namespace SemanticMapper.Internal.Parsing;

internal static class JsonFlattener
{
    public static SourceDocument Flatten(string json, int maxDepth)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = maxDepth,
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidSourceDocumentException("The source document is not valid JSON.", ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidSourceDocumentException("The root of a JSON source document must be an object.");
            }

            var builder = new SourceFieldCollector();
            VisitObject(document.RootElement, parent: null, builder);
            return builder.Build(SourceFormat.Json);
        }
    }

    private static void VisitObject(JsonElement element, string? parent, SourceFieldCollector collector)
    {
        foreach (var property in element.EnumerateObject())
        {
            var path = SourcePath.Append(parent, property.Name);
            var value = property.Value;
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    VisitObject(value, path, collector);
                    break;
                case JsonValueKind.Array:
                    VisitArray(value, path, property.Name, collector);
                    break;
                default:
                    var (kind, text) = Scalar(value);
                    collector.Add(new SourceField(path, property.Name, kind, text));
                    break;
            }
        }
    }

    private static void VisitArray(JsonElement array, string path, string name, SourceFieldCollector collector)
    {
        var items = new List<string?>();
        var kinds = new List<FieldValueKind>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                collector.AddUnsupported(path);
                return;
            }

            var (kind, text) = Scalar(item);
            items.Add(text);
            kinds.Add(kind);
        }

        collector.Add(new SourceField(path, name, ValueKindInference.Common(kinds), items));
    }

    private static (FieldValueKind Kind, string? Text) Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => (ValueKindInference.FromString(value.GetString()!), value.GetString()),
        JsonValueKind.Number => (value.TryGetInt64(out _) ? FieldValueKind.Integer : FieldValueKind.Decimal, value.GetRawText()),
        JsonValueKind.True => (FieldValueKind.Boolean, "true"),
        JsonValueKind.False => (FieldValueKind.Boolean, "false"),
        _ => (FieldValueKind.Null, null),
    };
}
