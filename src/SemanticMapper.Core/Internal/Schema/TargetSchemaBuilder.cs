using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SemanticMapper.Exceptions;
using SemanticMapper.Matching;

namespace SemanticMapper.Internal.Schema;

/// <summary>Builds <see cref="TargetSchema"/> by reflecting over a destination type.</summary>
internal static class TargetSchemaBuilder
{
    public static TargetSchema Build(Type type, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(type);
        EnsureConstructible(type, type);
        if (TypeClassifier.TryGetScalarKind(type, out _) || TypeClassifier.TryGetScalarCollection(type, out _, out _))
        {
            throw new UnsupportedDestinationTypeException(type, "the destination must be an object with properties, not a scalar or collection.");
        }

        var walk = new SchemaWalk(type, maxDepth);
        Visit(type, pathPrefix: null, [], depth: 1, walk);
        var members = walk.Members;

        if (members.Count == 0)
        {
            throw new UnsupportedDestinationTypeException(type, "it has no public settable properties that can be mapped.");
        }

        return new TargetSchema(type, members, ComputeFingerprint(type.FullName ?? type.Name, members.Select(m => m.Field)));
    }

    public static string ComputeFingerprint(string typeName, IEnumerable<TargetField> fields)
    {
        var builder = new StringBuilder(typeName);
        foreach (var field in fields)
        {
            builder.Append('\n')
                .Append(field.Path).Append('|')
                .Append(field.PropertyType.FullName).Append('|')
                .Append(field.IsNullable ? '?' : '!');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void Visit(Type type, string? pathPrefix, List<PropertyInfo> chain, int depth, SchemaWalk walk)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.SetMethod is { IsPublic: true })
            .OrderBy(p => DeclarationDepth(p, type))
            .ThenBy(p => p.MetadataToken);

        foreach (var property in properties)
        {
            var path = pathPrefix is null ? property.Name : pathPrefix + "." + property.Name;
            var propertyChain = new List<PropertyInfo>(chain) { property };
            var propertyType = property.PropertyType;

            if (TypeClassifier.TryGetScalarKind(propertyType, out var kind))
            {
                walk.Members.Add(new TargetMember(CreateField(path, property, kind, isCollection: false, walk.Nullability), propertyChain, elementType: null));
            }
            else if (TypeClassifier.TryGetScalarCollection(propertyType, out var elementType, out var elementKind))
            {
                walk.Members.Add(new TargetMember(CreateField(path, property, elementKind, isCollection: true, walk.Nullability), propertyChain, elementType));
            }
            else if (IsNestedObject(propertyType) && depth < walk.MaxDepth && walk.Ancestors.Add(propertyType))
            {
                Visit(propertyType, path, propertyChain, depth + 1, walk);
                walk.Ancestors.Remove(propertyType);
            }
        }
    }

    private static TargetField CreateField(string path, PropertyInfo property, FieldValueKind kind, bool isCollection, NullabilityInfoContext nullability)
    {
        var type = property.PropertyType;
        var isNullable = type.IsValueType
            ? Nullable.GetUnderlyingType(type) is not null
            : nullability.Create(property).WriteState != NullabilityState.NotNull;
        return new TargetField(path, property.Name, type, kind, isCollection, isNullable, property.DeclaringType?.Name ?? "");
    }

    /// <summary>Base-class properties first, matching how developers read an inheritance hierarchy.</summary>
    private static int DeclarationDepth(PropertyInfo property, Type type)
    {
        var depth = 0;
        for (var current = type; current is not null && current != property.DeclaringType; current = current.BaseType)
        {
            depth--;
        }

        return depth;
    }

    private static bool IsNestedObject(Type type) =>
        type.IsClass && !type.IsAbstract && type != typeof(string) && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
        && type.GetConstructor(Type.EmptyTypes) is not null;

    private static void EnsureConstructible(Type root, Type type)
    {
        if (type.IsAbstract || type.IsInterface)
        {
            throw new UnsupportedDestinationTypeException(root, "abstract types and interfaces cannot be instantiated.");
        }

        if (type.ContainsGenericParameters)
        {
            throw new UnsupportedDestinationTypeException(root, "open generic types cannot be instantiated.");
        }

        if (!type.IsValueType && type.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new UnsupportedDestinationTypeException(root, "it must have a public parameterless constructor.");
        }
    }

    /// <summary>The state shared by every level of one schema walk.</summary>
    private sealed class SchemaWalk(Type root, int maxDepth)
    {
        public int MaxDepth { get; } = maxDepth;

        // NullabilityInfoContext is not thread-safe, so each build uses its own.
        public NullabilityInfoContext Nullability { get; } = new();

        public List<TargetMember> Members { get; } = [];

        /// <summary>The types on the current path, used to stop at recursive type graphs.</summary>
        public HashSet<Type> Ancestors { get; } = [root];
    }
}
