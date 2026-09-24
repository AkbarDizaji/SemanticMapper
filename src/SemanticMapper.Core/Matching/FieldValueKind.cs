using System.Diagnostics.CodeAnalysis;

namespace SemanticMapper;

/// <summary>
/// The normalized kind of a field value, shared by source and destination fields.
/// </summary>
/// <remarks>
/// For source fields this is inferred from the document; for destination fields it is derived
/// from the CLR property type. For arrays and collections it describes the element kind.
/// </remarks>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Kinds are named after the value categories they describe, like JsonValueKind.")]
public enum FieldValueKind
{
    /// <summary>The kind could not be determined.</summary>
    Unknown = 0,

    /// <summary>An explicit null value (source fields only).</summary>
    Null,

    /// <summary>Free text.</summary>
    String,

    /// <summary>A true/false value.</summary>
    Boolean,

    /// <summary>A whole number.</summary>
    Integer,

    /// <summary>A number with a fractional part.</summary>
    Decimal,

    /// <summary>A calendar date without a time component.</summary>
    Date,

    /// <summary>A date and time, optionally with an offset.</summary>
    DateTime,

    /// <summary>A time of day.</summary>
    Time,

    /// <summary>A length of time.</summary>
    Duration,

    /// <summary>A globally unique identifier.</summary>
    Guid,

    /// <summary>A value from a fixed set of named values.</summary>
    Enumeration,
}
