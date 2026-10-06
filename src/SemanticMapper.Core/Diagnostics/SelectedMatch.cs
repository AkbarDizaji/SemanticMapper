namespace SemanticMapper.Diagnostics;

/// <summary>The candidate selected for a source field, with the scores that justified it.</summary>
internal readonly record struct SelectedMatch(string TargetPath, double Confidence, double Gap);
