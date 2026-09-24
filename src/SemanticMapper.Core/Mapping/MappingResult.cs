namespace SemanticMapper;

/// <summary>
/// The outcome of a successful mapping.
/// </summary>
/// <typeparam name="T">The destination type.</typeparam>
public sealed class MappingResult<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MappingResult{T}"/> class.
    /// </summary>
    /// <param name="value">The mapped value.</param>
    /// <param name="diagnostics">Diagnostics for the mapping.</param>
    /// <param name="usedCachedPlan">Whether a cached mapping plan was reused.</param>
    public MappingResult(T value, MappingDiagnostics diagnostics, bool usedCachedPlan)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Value = value;
        Diagnostics = diagnostics;
        UsedCachedPlan = usedCachedPlan;
    }

    /// <summary>Gets the mapped value.</summary>
    public T Value { get; }

    /// <summary>Gets diagnostics describing every mapping decision.</summary>
    public MappingDiagnostics Diagnostics { get; }

    /// <summary>Gets a value indicating whether a cached plan was reused, meaning the semantic matcher was not called.</summary>
    public bool UsedCachedPlan { get; }
}
