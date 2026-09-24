namespace SemanticMapper;

/// <summary>Thrown when a source value cannot be converted to its destination property type.</summary>
/// <remarks>The message deliberately excludes the value, which may be sensitive.</remarks>
public class ValueConversionException : SemanticMapperException
{
    /// <summary>Initializes a new instance.</summary>
    public ValueConversionException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public ValueConversionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public ValueConversionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance for a failed conversion.</summary>
    /// <param name="sourcePath">The source field path.</param>
    /// <param name="targetPath">The destination property path.</param>
    /// <param name="targetType">The destination property type.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public ValueConversionException(string sourcePath, string targetPath, Type targetType, Exception? innerException = null)
        : base($"The value of source field '{sourcePath}' could not be converted to '{targetType?.Name}' for property '{targetPath}'.", innerException)
    {
        SourcePath = sourcePath;
        TargetPath = targetPath;
        TargetType = targetType;
    }

    /// <summary>Gets the source field path.</summary>
    public string? SourcePath { get; }

    /// <summary>Gets the destination property path.</summary>
    public string? TargetPath { get; }

    /// <summary>Gets the destination property type.</summary>
    public Type? TargetType { get; }
}
