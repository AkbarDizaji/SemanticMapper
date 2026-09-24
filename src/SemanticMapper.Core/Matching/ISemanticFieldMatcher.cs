namespace SemanticMapper;

/// <summary>
/// Scores how well a source field matches each destination candidate by semantic meaning.
/// </summary>
/// <remarks>
/// Implementations are provider adapters (for example the TypeSafe Jev provider in
/// <c>SemanticMapper.Jev</c>). The mapper calls this once per source field on a plan-cache
/// miss and resolves the final one-to-one assignment itself, so implementations only need to
/// score candidates independently. Implementations must be thread-safe.
/// </remarks>
public interface ISemanticFieldMatcher
{
    /// <summary>
    /// Gets a stable identifier for the provider, model and prompt version.
    /// </summary>
    /// <remarks>
    /// This value is part of the mapping-plan cache key. Change it whenever a change to the
    /// provider could produce different scores, so plans created by the old version are not reused.
    /// </remarks>
    string Version { get; }

    /// <summary>
    /// Scores the destination candidates for a single source field.
    /// </summary>
    /// <param name="source">The source field. Its <see cref="SourceField.Value"/> may contain sensitive data.</param>
    /// <param name="candidates">The destination fields the source may map to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A confidence score in [0, 1] per scored candidate.</returns>
    Task<FieldMatchResult> MatchAsync(
        SourceField source,
        IReadOnlyList<TargetField> candidates,
        CancellationToken cancellationToken = default);
}
