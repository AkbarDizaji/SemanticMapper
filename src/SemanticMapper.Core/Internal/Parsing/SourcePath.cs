namespace SemanticMapper.Internal.Parsing;

/// <summary>Builds normalized, unambiguous field paths shared by the JSON and XML flatteners.</summary>
internal static class SourcePath
{
    public static string Append(string? parent, string segment)
    {
        var escaped = NeedsQuoting(segment)
            ? "['" + segment.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal) + "']"
            : segment;

        if (string.IsNullOrEmpty(parent))
        {
            return escaped;
        }

        return escaped[0] == '[' ? parent + escaped : parent + "." + escaped;
    }

    private static bool NeedsQuoting(string segment) =>
        segment.Length == 0 || segment.AsSpan().IndexOfAny(".[]'\\") >= 0;
}
