namespace SemanticMapper.Internal.Parsing;

/// <summary>Accumulates flattened fields in document order and rejects duplicate paths.</summary>
internal sealed class SourceFieldCollector
{
    private readonly List<SourceField> _fields = [];
    private readonly List<string> _unsupported = [];
    private readonly HashSet<string> _paths = new(StringComparer.Ordinal);

    public void Add(SourceField field)
    {
        if (!_paths.Add(field.Path))
        {
            throw new InvalidSourceDocumentException($"The source document contains the field path '{field.Path}' more than once.");
        }

        _fields.Add(field);
    }

    public void AddUnsupported(string path)
    {
        if (!_paths.Add(path))
        {
            throw new InvalidSourceDocumentException($"The source document contains the field path '{path}' more than once.");
        }

        _unsupported.Add(path);
    }

    public SourceDocument Build(SourceFormat format) => new(format, _fields, _unsupported);
}
