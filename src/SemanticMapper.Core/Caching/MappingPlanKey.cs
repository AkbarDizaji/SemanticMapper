namespace SemanticMapper;

/// <summary>
/// A stable key identifying a mapping plan.
/// </summary>
/// <remarks>
/// The key is a hash of the source field paths, the destination type's schema, the matcher version and
/// the confidence policy. It is stable across processes, so it can be used with distributed caches.
/// </remarks>
/// <param name="Value">The key text.</param>
public readonly record struct MappingPlanKey(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}
