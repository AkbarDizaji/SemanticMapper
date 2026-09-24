using Microsoft.Extensions.Logging;

namespace SemanticMapper.Internal.Logging;

/// <summary>
/// Structured log events. Only paths, type names, scores and policy values are logged; source field
/// values are never passed to the logger because they may contain sensitive data.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1, EventName = "PlanCacheHit", Level = LogLevel.Debug,
        Message = "Mapping plan cache hit for {DestinationType} (key {PlanKey})")]
    public static partial void PlanCacheHit(ILogger logger, string destinationType, string planKey);

    [LoggerMessage(EventId = 2, EventName = "PlanCacheMiss", Level = LogLevel.Debug,
        Message = "Mapping plan cache miss for {DestinationType} (key {PlanKey}); building a plan with {SourceFieldCount} source fields and matcher {MatcherVersion}")]
    public static partial void PlanCacheMiss(ILogger logger, string destinationType, string planKey, int sourceFieldCount, string matcherVersion);

    [LoggerMessage(EventId = 3, EventName = "SemanticMatchPerformed", Level = LogLevel.Debug,
        Message = "Semantic match performed for source field {SourcePath} against {CandidateCount} candidates; best {BestCandidate} ({BestConfidence:0.00})")]
    public static partial void SemanticMatchPerformed(ILogger logger, string sourcePath, int candidateCount, string? bestCandidate, double bestConfidence);

    [LoggerMessage(EventId = 4, EventName = "CandidateSelected", Level = LogLevel.Debug,
        Message = "Source field {SourcePath} mapped to {TargetPath} with confidence {Confidence:0.00} and gap {ConfidenceGap:0.00} ({Status})")]
    public static partial void CandidateSelected(ILogger logger, string sourcePath, string targetPath, double confidence, double confidenceGap, MatchStatus status);

    [LoggerMessage(EventId = 5, EventName = "CandidateRejected", Level = LogLevel.Debug,
        Message = "Candidate {TargetPath} rejected for source field {SourcePath}: {Reason}")]
    public static partial void CandidateRejected(ILogger logger, string targetPath, string sourcePath, string reason);

    [LoggerMessage(EventId = 6, EventName = "LowConfidenceMatch", Level = LogLevel.Warning,
        Message = "Low-confidence match for source field {SourcePath}: {TargetPath} scored {Confidence:0.00}, below the threshold {MinimumConfidence:0.00}; applying {Behavior}")]
    public static partial void LowConfidence(ILogger logger, string sourcePath, string targetPath, double confidence, double minimumConfidence, MappingFailureBehavior behavior);

    [LoggerMessage(EventId = 7, EventName = "AmbiguousMatch", Level = LogLevel.Warning,
        Message = "Ambiguous match for source field {SourcePath}: {TargetPath} scored {Confidence:0.00} with gap {ConfidenceGap:0.00}, below the required gap {MinimumConfidenceGap:0.00}; applying {Behavior}")]
    public static partial void Ambiguous(ILogger logger, string sourcePath, string targetPath, double confidence, double confidenceGap, double minimumConfidenceGap, MappingFailureBehavior behavior);

    [LoggerMessage(EventId = 8, EventName = "NoSemanticMatch", Level = LogLevel.Debug,
        Message = "No destination property matches source field {SourcePath}")]
    public static partial void NoMatch(ILogger logger, string sourcePath);

    [LoggerMessage(EventId = 9, EventName = "UnsupportedSourceField", Level = LogLevel.Debug,
        Message = "Source field {SourcePath} was skipped because its shape is not supported")]
    public static partial void UnsupportedSourceField(ILogger logger, string sourcePath);
}
