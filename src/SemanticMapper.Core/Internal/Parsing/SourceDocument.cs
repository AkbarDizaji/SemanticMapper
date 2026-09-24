namespace SemanticMapper.Internal.Parsing;

/// <summary>A parsed document normalized into leaf fields, independent of its original format.</summary>
internal sealed record SourceDocument(
    SourceFormat Format,
    IReadOnlyList<SourceField> Fields,
    IReadOnlyList<string> UnsupportedPaths);
