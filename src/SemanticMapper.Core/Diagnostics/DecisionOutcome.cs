namespace SemanticMapper;

/// <summary>What the mapper did with a source field.</summary>
public enum DecisionOutcome
{
    /// <summary>The source value is written to the selected destination property.</summary>
    Applied = 0,

    /// <summary>The match was rejected and the destination property keeps its default value.</summary>
    LeftDefault,

    /// <summary>The match was rejected and caused the mapping to fail.</summary>
    Failed,

    /// <summary>The source field has no destination property (see <see cref="MatchStatus.NoMatch"/> and <see cref="MatchStatus.Conflict"/>).</summary>
    Unmapped,
}
