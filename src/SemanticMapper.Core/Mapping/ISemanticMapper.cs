namespace SemanticMapper;

/// <summary>
/// Maps JSON or XML documents into C# models by the semantic meaning of their field names.
/// </summary>
public interface ISemanticMapper
{
    /// <summary>
    /// Maps a JSON or XML document into a new instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The destination type. It must be a concrete type with a public parameterless constructor
    /// and settable (or <c>init</c>) properties.
    /// </typeparam>
    /// <param name="jsonOrXml">The document. The format is detected automatically.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The mapped value, diagnostics for every mapping decision, and whether a cached plan was used.</returns>
    /// <exception cref="InvalidSourceDocumentException">The input is not a supported JSON or XML document.</exception>
    /// <exception cref="UnsupportedDestinationTypeException"><typeparamref name="T"/> cannot be mapped into.</exception>
    /// <exception cref="UnsafeMappingException">A match violated the confidence rules and the configured behavior is <see cref="MappingFailureBehavior.Throw"/>.</exception>
    /// <exception cref="ValueConversionException">A source value could not be converted to its destination property type.</exception>
    /// <exception cref="SemanticMatcherException">The semantic matcher failed.</exception>
    Task<MappingResult<T>> MapAsync<T>(string jsonOrXml, CancellationToken cancellationToken = default);
}
