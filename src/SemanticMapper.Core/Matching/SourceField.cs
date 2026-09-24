namespace SemanticMapper;

/// <summary>
/// A leaf field extracted from a JSON or XML source document.
/// </summary>
/// <remarks>
/// JSON and XML inputs are normalized into the same representation, so matchers never see the
/// original format. <see cref="ToString"/> returns only the path so that instances can be logged
/// without exposing values.
/// </remarks>
public sealed class SourceField
{
    /// <summary>
    /// Initializes a new scalar source field.
    /// </summary>
    /// <param name="path">The normalized path, for example <c>customer.address.city</c>.</param>
    /// <param name="name">The field's own name, for example <c>city</c>.</param>
    /// <param name="valueKind">The inferred kind of the value.</param>
    /// <param name="value">The value as text, or <see langword="null"/>.</param>
    public SourceField(string path, string name, FieldValueKind valueKind, string? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(name);
        Path = path;
        Name = name;
        ValueKind = valueKind;
        Value = value;
        Items = [];
    }

    /// <summary>
    /// Initializes a new array source field whose items are scalar values.
    /// </summary>
    /// <param name="path">The normalized path.</param>
    /// <param name="name">The field's own name.</param>
    /// <param name="elementKind">The inferred kind shared by the items.</param>
    /// <param name="items">The item values as text.</param>
    public SourceField(string path, string name, FieldValueKind elementKind, IReadOnlyList<string?> items)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(items);
        Path = path;
        Name = name;
        ValueKind = elementKind;
        Items = items;
        IsArray = true;
    }

    /// <summary>Gets the normalized path that uniquely identifies the field in the document.</summary>
    public string Path { get; }

    /// <summary>Gets the field's own name (the last path segment).</summary>
    public string Name { get; }

    /// <summary>Gets the inferred value kind, or the element kind when <see cref="IsArray"/> is true.</summary>
    public FieldValueKind ValueKind { get; }

    /// <summary>Gets a value indicating whether the field is an array of scalar values.</summary>
    public bool IsArray { get; }

    /// <summary>Gets the scalar value as text. Always <see langword="null"/> for arrays. May be sensitive.</summary>
    public string? Value { get; }

    /// <summary>Gets the array item values as text. Empty for scalar fields. May be sensitive.</summary>
    public IReadOnlyList<string?> Items { get; }

    /// <summary>Returns the field path. Values are deliberately excluded.</summary>
    public override string ToString() => Path;
}
