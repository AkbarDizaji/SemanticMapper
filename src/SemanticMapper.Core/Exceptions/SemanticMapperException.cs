namespace SemanticMapper;

/// <summary>The base type for exceptions thrown by SemanticMapper.</summary>
public class SemanticMapperException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    public SemanticMapperException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public SemanticMapperException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public SemanticMapperException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
