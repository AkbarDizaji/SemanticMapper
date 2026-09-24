namespace SemanticMapper;

/// <summary>
/// Thrown when a semantic matcher fails or returns an invalid result. Provider packages derive
/// their exceptions from this type.
/// </summary>
public class SemanticMatcherException : SemanticMapperException
{
    /// <summary>Initializes a new instance.</summary>
    public SemanticMatcherException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public SemanticMatcherException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public SemanticMatcherException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
