using System.Globalization;
using System.Text;

namespace SemanticMapper;

/// <summary>
/// Thrown when one or more matches violate the confidence rules and the configured
/// <see cref="MappingFailureBehavior"/> is <see cref="MappingFailureBehavior.Throw"/>.
/// </summary>
/// <remarks>The message and diagnostics contain field paths and scores but never source values.</remarks>
public class UnsafeMappingException : SemanticMapperException
{
    /// <summary>Initializes a new instance.</summary>
    public UnsafeMappingException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public UnsafeMappingException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public UnsafeMappingException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance from the mapping diagnostics.</summary>
    /// <param name="diagnostics">The diagnostics, including the failed decisions.</param>
    public UnsafeMappingException(MappingDiagnostics diagnostics)
        : base(BuildMessage(diagnostics))
    {
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the diagnostics for the failed mapping.</summary>
    public MappingDiagnostics? Diagnostics { get; }

    /// <summary>Gets the decisions that caused the failure.</summary>
    public IReadOnlyList<FieldMappingDecision> FailedDecisions =>
        Diagnostics?.Decisions.Where(d => d.Outcome == DecisionOutcome.Failed).ToList() ?? [];

    private static string BuildMessage(MappingDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var culture = CultureInfo.InvariantCulture;
        var builder = new StringBuilder()
            .Append(culture, $"Semantic mapping to '{diagnostics.DestinationType.FullName}' was rejected by the confidence rules:");
        foreach (var decision in diagnostics.Decisions.Where(d => d.Outcome == DecisionOutcome.Failed))
        {
            builder.AppendLine().Append(
                culture,
                $"  {decision.SourcePath} -> {decision.SelectedTargetPath}: {decision.Status} (confidence {decision.SelectedConfidence:0.00}, gap {decision.ConfidenceGap:0.00}; required {decision.MinimumConfidence:0.00} / {decision.MinimumConfidenceGap:0.00})");
        }

        return builder.ToString();
    }
}
