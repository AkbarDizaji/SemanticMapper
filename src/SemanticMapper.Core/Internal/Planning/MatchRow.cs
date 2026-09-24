namespace SemanticMapper.Internal.Planning;

/// <summary>One row of the candidate matrix: a source field and its validated scores.</summary>
internal sealed record MatchRow(SourceField Source, IReadOnlyList<CandidateScore> Candidates, double? NoMatchConfidence)
{
    public double BestConfidence => Candidates.Count == 0 ? 0 : Candidates[0].Confidence;

    /// <summary>True when the field has no counterpart, so it takes no part in the assignment.</summary>
    public bool IsNoMatch => BestConfidence <= 0 || NoMatchConfidence > BestConfidence;
}
