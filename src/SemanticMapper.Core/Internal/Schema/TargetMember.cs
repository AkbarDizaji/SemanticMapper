using System.Reflection;

namespace SemanticMapper.Internal.Schema;

/// <summary>A destination leaf property plus the property chain needed to reach it from the root.</summary>
internal sealed class TargetMember(TargetField field, IReadOnlyList<PropertyInfo> chain, Type? elementType)
{
    public TargetField Field { get; } = field;

    /// <summary>The properties from the root type to the leaf; the last entry is the leaf itself.</summary>
    public IReadOnlyList<PropertyInfo> Chain { get; } = chain;

    /// <summary>The collection element type, or null for scalar properties.</summary>
    public Type? ElementType { get; } = elementType;

    public PropertyInfo Property => Chain[^1];
}
