namespace SemanticMapper.Internal.Schema;

/// <summary>Reflection metadata for a destination type.</summary>
internal sealed class TargetSchema
{
    private readonly Dictionary<string, TargetMember> _byPath;

    public TargetSchema(Type type, IReadOnlyList<TargetMember> members, string fingerprint)
    {
        Type = type;
        Members = members;
        Fields = [.. members.Select(m => m.Field)];
        Fingerprint = fingerprint;
        _byPath = members.ToDictionary(m => m.Field.Path, StringComparer.Ordinal);
    }

    public Type Type { get; }

    public IReadOnlyList<TargetMember> Members { get; }

    public IReadOnlyList<TargetField> Fields { get; }

    /// <summary>A stable hash of the type name and every field's path and type.</summary>
    public string Fingerprint { get; }

    public TargetMember? Find(string path) => _byPath.GetValueOrDefault(path);

    public object CreateInstance() => Activator.CreateInstance(Type)!;
}
