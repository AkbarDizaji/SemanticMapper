namespace SemanticMapper.Internal.Schema;

/// <summary>Classifies CLR types into scalar kinds and supported collections.</summary>
internal static class TypeClassifier
{
    public static bool TryGetScalarKind(Type type, out FieldValueKind kind)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying.IsEnum)
        {
            kind = FieldValueKind.Enumeration;
            return true;
        }

        kind = Type.GetTypeCode(underlying) switch
        {
            TypeCode.String or TypeCode.Char => FieldValueKind.String,
            TypeCode.Boolean => FieldValueKind.Boolean,
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Int64 or TypeCode.UInt64 => FieldValueKind.Integer,
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => FieldValueKind.Decimal,
            TypeCode.DateTime => FieldValueKind.DateTime,
            _ => FieldValueKind.Unknown,
        };

        if (kind != FieldValueKind.Unknown)
        {
            return true;
        }

        kind = underlying switch
        {
            _ when underlying == typeof(DateTimeOffset) => FieldValueKind.DateTime,
            _ when underlying == typeof(DateOnly) => FieldValueKind.Date,
            _ when underlying == typeof(TimeOnly) => FieldValueKind.Time,
            _ when underlying == typeof(TimeSpan) => FieldValueKind.Duration,
            _ when underlying == typeof(Guid) => FieldValueKind.Guid,
            _ when underlying == typeof(Uri) => FieldValueKind.String,
            _ => FieldValueKind.Unknown,
        };
        return kind != FieldValueKind.Unknown;
    }

    /// <summary>
    /// Recognizes arrays, <see cref="List{T}"/> and the interfaces <see cref="List{T}"/> implements,
    /// whose element type is a scalar.
    /// </summary>
    public static bool TryGetScalarCollection(Type type, out Type elementType, out FieldValueKind elementKind)
    {
        elementType = typeof(object);
        elementKind = FieldValueKind.Unknown;

        Type? candidate = null;
        if (type.IsArray && type.GetArrayRank() == 1)
        {
            candidate = type.GetElementType();
        }
        else if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(ICollection<>)
                || definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyList<>) || definition == typeof(IReadOnlyCollection<>))
            {
                candidate = type.GetGenericArguments()[0];
            }
        }

        if (candidate is null || !TryGetScalarKind(candidate, out elementKind))
        {
            return false;
        }

        elementType = candidate;
        return true;
    }
}
