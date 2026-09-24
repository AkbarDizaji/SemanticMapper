using System.Collections;
using System.Globalization;
using System.Xml;
using SemanticMapper.Internal.Schema;

namespace SemanticMapper.Internal.Conversion;

/// <summary>Converts normalized source text into destination property values using the invariant culture.</summary>
internal static class ValueConverter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Converts a source field for a destination member.</summary>
    /// <returns>False when the value is null and the property is a non-nullable value type, meaning it keeps its default.</returns>
    public static bool TryConvert(SourceField source, TargetMember member, out object? result)
    {
        try
        {
            if (member.ElementType is { } elementType)
            {
                result = ConvertCollection(source, member.Field.PropertyType, elementType);
                return true;
            }

            if (source.IsArray)
            {
                throw new FormatException("An array cannot be assigned to a scalar property.");
            }

            var type = member.Field.PropertyType;
            if (source.Value is null)
            {
                result = null;
                return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
            }

            result = ConvertScalar(source.Value, type);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or InvalidCastException)
        {
            // The inner exception message may echo the value, so it is not attached.
            throw new ValueConversionException(source.Path, member.Field.Path, member.Field.PropertyType);
        }
    }

    private static object ConvertCollection(SourceField source, Type collectionType, Type elementType)
    {
        if (!source.IsArray)
        {
            throw new FormatException("A scalar cannot be assigned to a collection property.");
        }

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var item in source.Items)
        {
            if (item is null)
            {
                if (elementType.IsValueType && Nullable.GetUnderlyingType(elementType) is null)
                {
                    throw new FormatException("A null item cannot be added to a collection of non-nullable values.");
                }

                list.Add(null);
            }
            else
            {
                list.Add(ConvertScalar(item, elementType));
            }
        }

        if (collectionType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        return list;
    }

    private static object ConvertScalar(string text, Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;

        if (target == typeof(string))
        {
            return text;
        }

        if (target.IsEnum)
        {
            return ConvertEnum(text, target);
        }

        switch (Type.GetTypeCode(target))
        {
            case TypeCode.Boolean:
                return text switch
                {
                    "1" => true,
                    "0" => false,
                    _ => bool.Parse(text),
                };
            case TypeCode.Char:
                return text.Length == 1 ? text[0] : throw new FormatException();
            case TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64:
                return ConvertInteger(text, target);
            case TypeCode.Single:
                return float.Parse(text, NumberStyles.Float, Invariant);
            case TypeCode.Double:
                return double.Parse(text, NumberStyles.Float, Invariant);
            case TypeCode.Decimal:
                return decimal.Parse(text, NumberStyles.Float, Invariant);
            case TypeCode.DateTime:
                return DateTime.Parse(text, Invariant, DateTimeStyles.RoundtripKind);
        }

        if (target == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(text, Invariant, DateTimeStyles.AssumeUniversal);
        }

        if (target == typeof(DateOnly))
        {
            return DateOnly.TryParse(text, Invariant, DateTimeStyles.None, out var date)
                ? date
                : DateOnly.FromDateTime(DateTime.Parse(text, Invariant, DateTimeStyles.RoundtripKind));
        }

        if (target == typeof(TimeOnly))
        {
            return TimeOnly.Parse(text, Invariant);
        }

        if (target == typeof(TimeSpan))
        {
            // Accept both "01:30:00" and ISO 8601 / XML durations such as "PT1H30M".
            return TimeSpan.TryParse(text, Invariant, out var span) ? span : XmlConvert.ToTimeSpan(text);
        }

        if (target == typeof(Guid))
        {
            return Guid.Parse(text);
        }

        if (target == typeof(Uri))
        {
            return Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri) ? uri : throw new FormatException();
        }

        throw new FormatException();
    }

    private static object ConvertInteger(string text, Type target)
    {
        // Parse as decimal so "3.0" is accepted, then reject fractions instead of rounding.
        var value = decimal.Parse(text, NumberStyles.Float, Invariant);
        if (decimal.Truncate(value) != value)
        {
            throw new FormatException();
        }

        return System.Convert.ChangeType(value, target, Invariant);
    }

    private static object ConvertEnum(string text, Type target)
    {
        if (!Enum.TryParse(target, text, ignoreCase: true, out var value))
        {
            throw new FormatException();
        }

        var isFlags = target.IsDefined(typeof(FlagsAttribute), inherit: false);
        return isFlags || Enum.IsDefined(target, value) ? value : throw new FormatException();
    }
}
