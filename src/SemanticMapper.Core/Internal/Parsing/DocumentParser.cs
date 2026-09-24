namespace SemanticMapper.Internal.Parsing;

/// <summary>Detects the document format and flattens it into source fields.</summary>
internal static class DocumentParser
{
    public static SourceDocument Parse(string input, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(input);

        var start = 0;
        while (start < input.Length && (char.IsWhiteSpace(input[start]) || input[start] == '﻿'))
        {
            start++;
        }

        if (start == input.Length)
        {
            throw new InvalidSourceDocumentException("The source document is empty.");
        }

        var content = start == 0 ? input : input[start..];
        return content[0] switch
        {
            '{' or '[' => JsonFlattener.Flatten(content, maxDepth),
            '<' => XmlFlattener.Flatten(content, maxDepth),
            _ => throw new InvalidSourceDocumentException("The source document is neither JSON nor XML."),
        };
    }
}
