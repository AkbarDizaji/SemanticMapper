namespace SemanticMapper;

/// <summary>
/// Metadata about a settable property on the destination type that a source field can map to.
/// </summary>
public sealed class TargetField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TargetField"/> class.
    /// </summary>
    /// <param name="path">The property path from the destination root, for example <c>Address.City</c>.</param>
    /// <param name="name">The property name, for example <c>City</c>.</param>
    /// <param name="propertyType">The declared CLR type of the property.</param>
    /// <param name="valueKind">The value kind, or the element kind for collections.</param>
    /// <param name="isCollection">Whether the property is a collection of scalar values.</param>
    /// <param name="isNullable">Whether the property accepts <see langword="null"/>.</param>
    /// <param name="declaringTypeName">The name of the type that declares the property.</param>
    public TargetField(
        string path,
        string name,
        Type propertyType,
        FieldValueKind valueKind,
        bool isCollection,
        bool isNullable,
        string declaringTypeName)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(propertyType);
        ArgumentException.ThrowIfNullOrEmpty(declaringTypeName);
        Path = path;
        Name = name;
        PropertyType = propertyType;
        ValueKind = valueKind;
        IsCollection = isCollection;
        IsNullable = isNullable;
        DeclaringTypeName = declaringTypeName;
    }

    /// <summary>Gets the property path from the destination root.</summary>
    public string Path { get; }

    /// <summary>Gets the property name.</summary>
    public string Name { get; }

    /// <summary>Gets the declared CLR type of the property.</summary>
    public Type PropertyType { get; }

    /// <summary>Gets the value kind, or the element kind when <see cref="IsCollection"/> is true.</summary>
    public FieldValueKind ValueKind { get; }

    /// <summary>Gets a value indicating whether the property is a collection of scalar values.</summary>
    public bool IsCollection { get; }

    /// <summary>Gets a value indicating whether the property accepts <see langword="null"/>.</summary>
    public bool IsNullable { get; }

    /// <summary>Gets the name of the type that declares the property.</summary>
    public string DeclaringTypeName { get; }

    /// <inheritdoc />
    public override string ToString() => Path;
}
