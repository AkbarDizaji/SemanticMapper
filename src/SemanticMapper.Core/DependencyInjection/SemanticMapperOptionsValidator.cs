using Microsoft.Extensions.Options;

namespace SemanticMapper;

internal sealed class SemanticMapperOptionsValidator : IValidateOptions<SemanticMapperOptions>
{
    public ValidateOptionsResult Validate(string? name, SemanticMapperOptions options)
    {
        var failures = new List<string>();
        if (!IsUnitInterval(options.MinimumConfidence))
        {
            failures.Add($"{nameof(SemanticMapperOptions.MinimumConfidence)} must be between 0 and 1.");
        }

        if (!IsUnitInterval(options.MinimumConfidenceGap))
        {
            failures.Add($"{nameof(SemanticMapperOptions.MinimumConfidenceGap)} must be between 0 and 1.");
        }

        if (!Enum.IsDefined(options.OnLowConfidence))
        {
            failures.Add($"{nameof(SemanticMapperOptions.OnLowConfidence)} is not a valid {nameof(MappingFailureBehavior)}.");
        }

        if (!Enum.IsDefined(options.OnAmbiguousMatch))
        {
            failures.Add($"{nameof(SemanticMapperOptions.OnAmbiguousMatch)} is not a valid {nameof(MappingFailureBehavior)}.");
        }

        if (options.MaxConcurrentMatches < 1)
        {
            failures.Add($"{nameof(SemanticMapperOptions.MaxConcurrentMatches)} must be at least 1.");
        }

        if (options.MaxDepth < 1)
        {
            failures.Add($"{nameof(SemanticMapperOptions.MaxDepth)} must be at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsUnitInterval(double value) => value is >= 0 and <= 1;
}
