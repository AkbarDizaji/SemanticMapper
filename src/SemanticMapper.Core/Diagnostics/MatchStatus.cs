namespace SemanticMapper;

/// <summary>How a source field's selected match relates to the confidence rules.</summary>
public enum MatchStatus
{
    /// <summary>The match satisfies both the confidence threshold and the confidence gap.</summary>
    Safe = 0,

    /// <summary>The selected candidate is below the confidence threshold.</summary>
    LowConfidence,

    /// <summary>The selected candidate does not beat the next-best available candidate by the required gap.</summary>
    Ambiguous,

    /// <summary>
    /// The field has no counterpart: there were no compatible candidates, every candidate scored zero, or
    /// the matcher was more confident that nothing matches than in any candidate.
    /// </summary>
    NoMatch,

    /// <summary>Every candidate for the field was claimed by a source field with a higher score.</summary>
    Conflict,
}
