using System.Xml;
using System.Xml.Linq;
using SemanticMapper.Diagnostics;
using SemanticMapper.Exceptions;
using SemanticMapper.Matching;

namespace SemanticMapper.Internal.Parsing;

/// <summary>
/// Flattens XML into the same shape as JSON: the document element is the root object, child elements
/// are properties, attributes are <c>@name</c> properties and repeated simple elements are arrays.
/// </summary>
internal static class XmlFlattener
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    public static SourceDocument Flatten(string xml, int maxDepth)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
            };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new InvalidSourceDocumentException("The source document is not valid XML.", ex);
        }

        var walk = new XmlWalk(maxDepth, new SourceFieldCollector());
        VisitElement(document.Root!, parent: null, depth: 1, walk);
        return walk.Collector.Build(SourceFormat.Xml);
    }

    private static void VisitElement(XElement element, string? parent, int depth, XmlWalk walk)
    {
        if (depth > walk.MaxDepth)
        {
            throw new InvalidSourceDocumentException($"The source document exceeds the maximum depth of {walk.MaxDepth}.");
        }

        var collector = walk.Collector;
        AddAttributes(element, parent, collector);

        // Group repeated siblings while keeping the order of first appearance.
        foreach (var group in element.Elements().GroupBy(e => e.Name.LocalName, StringComparer.Ordinal))
        {
            var name = group.Key;
            var path = SourcePath.Append(parent, name);
            var elements = group.ToList();
            if (elements.Count > 1)
            {
                if (elements.All(IsSimple))
                {
                    var items = elements.Select(TextOf).ToList();
                    var kind = ValueKindInference.Common(items.Select(i => i is null ? FieldValueKind.Null : ValueKindInference.FromText(i)));
                    collector.Add(new SourceField(path, name, kind, items));
                }
                else
                {
                    collector.AddUnsupported(path);
                }

                continue;
            }

            var child = elements[0];
            if (child.HasElements)
            {
                VisitElement(child, path, depth + 1, walk);
                continue;
            }

            var text = TextOf(child);
            collector.Add(new SourceField(path, name, text is null ? FieldValueKind.Null : ValueKindInference.FromText(text), text));
            AddAttributes(child, path, collector);
        }
    }

    private static void AddAttributes(XElement element, string? parent, SourceFieldCollector collector)
    {
        foreach (var attribute in element.Attributes().Where(IsDataAttribute))
        {
            var name = attribute.Name.LocalName;
            collector.Add(new SourceField(SourcePath.Append(parent, "@" + name), name, ValueKindInference.FromText(attribute.Value), attribute.Value));
        }
    }

    private static bool IsSimple(XElement element) => !element.HasElements && !element.Attributes().Any(IsDataAttribute);

    /// <summary>Namespace declarations and <c>xsi:*</c> attributes are markup, not data.</summary>
    private static bool IsDataAttribute(XAttribute attribute) => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace != Xsi;

    /// <summary>Returns the element text, or null for empty and <c>xsi:nil</c> elements.</summary>
    private static string? TextOf(XElement element)
    {
        if (string.Equals((string?)element.Attribute(Xsi + "nil"), "true", StringComparison.Ordinal))
        {
            return null;
        }

        return element.IsEmpty || element.Value.Length == 0 ? null : element.Value;
    }

    private sealed record XmlWalk(int MaxDepth, SourceFieldCollector Collector);
}
