namespace SemanticMapper;

/// <summary>
/// A matcher's confidence that a source field corresponds to a destination candidate.
/// </summary>
/// <param name="Target">The destination candidate.</param>
/// <param name="Confidence">The confidence in [0, 1].</param>
public readonly record struct CandidateScore(TargetField Target, double Confidence);
