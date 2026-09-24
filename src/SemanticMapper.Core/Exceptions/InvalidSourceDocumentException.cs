namespace SemanticMapper;

/// <summary>Thrown when the input is not a supported JSON or XML document.</summary>
public class InvalidSourceDocumentException : SemanticMapperException
{
    /// <summary>Initializes a new instance.</summary>
    public InvalidSourceDocumentException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public InvalidSourceDocumentException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The parser exception.</param>
    public InvalidSourceDocumentException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
