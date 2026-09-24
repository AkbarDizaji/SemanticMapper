namespace SemanticMapper;

/// <summary>Thrown when a destination type cannot be constructed or has no mappable properties.</summary>
public class UnsupportedDestinationTypeException : SemanticMapperException
{
    /// <summary>Initializes a new instance.</summary>
    public UnsupportedDestinationTypeException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public UnsupportedDestinationTypeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public UnsupportedDestinationTypeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance for a destination type.</summary>
    /// <param name="destinationType">The unsupported type.</param>
    /// <param name="reason">Why the type is unsupported.</param>
    public UnsupportedDestinationTypeException(Type destinationType, string reason)
        : base($"Type '{destinationType?.FullName}' cannot be used as a mapping destination: {reason}")
    {
        DestinationType = destinationType;
    }

    /// <summary>Gets the unsupported type.</summary>
    public Type? DestinationType { get; }
}
