using System.Globalization;

namespace SemanticMapper.Internal.Parsing;

/// <summary>Infers a <see cref="FieldValueKind"/> from textual values.</summary>
internal static class ValueKindInference
{
    /// <summary>Infers the kind of a JSON string, which is text unless it has a well-known typed shape.</summary>
    public static FieldValueKind FromString(string value)
    {
        if (value.Length == 36 && Guid.TryParseExact(value, "D", out _))
        {
            return FieldValueKind.Guid;
        }

        if (value.Length == 10 && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return FieldValueKind.Date;
        }

        if (IsIsoDateTime(value))
        {
            return FieldValueKind.DateTime;
        }

        if (value.Length is 5 or 8 && TimeOnly.TryParseExact(value, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return FieldValueKind.Time;
        }

        return FieldValueKind.String;
    }

    /// <summary>Infers the kind of untyped text such as XML element content.</summary>
    public static FieldValueKind FromText(string value)
    {
        if (bool.TryParse(value, out _))
        {
            return FieldValueKind.Boolean;
        }

        if (long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
        {
            return FieldValueKind.Integer;
        }

        if (decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
        {
            return FieldValueKind.Decimal;
        }

        return FromString(value);
    }

    /// <summary>Returns the shared kind of array items, or <see cref="FieldValueKind.String"/> when they differ.</summary>
    public static FieldValueKind Common(IEnumerable<FieldValueKind> kinds)
    {
        FieldValueKind? common = null;
        foreach (var kind in kinds)
        {
            if (kind == FieldValueKind.Null)
            {
                continue;
            }

            if (common is null)
            {
                common = kind;
            }
            else if (common != kind)
            {
                // Integers and decimals mix naturally in numeric arrays.
                common = IsNumeric(common.Value) && IsNumeric(kind) ? FieldValueKind.Decimal : FieldValueKind.String;
            }
        }

        return common ?? FieldValueKind.Unknown;
    }

    private static bool IsNumeric(FieldValueKind kind) => kind is FieldValueKind.Integer or FieldValueKind.Decimal;

    private static bool IsIsoDateTime(string value) =>
        value.Length >= 16 &&
        value[4] == '-' && value[7] == '-' && value[10] is 'T' or ' ' &&
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);
}
