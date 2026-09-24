using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SemanticMapper.Internal.Planning;

/// <summary>
/// Creates the mapping-plan cache key from everything that can change a plan: the source field
/// paths, the destination schema, the matcher version and the confidence policy.
/// </summary>
/// <remarks>
/// Source value kinds are deliberately excluded so that, for example, a field that is null in one
/// document and populated in the next still reuses the same plan.
/// </remarks>
internal static class PlanKeyFactory
{
    private const string FormatVersion = "semanticmapper-plan-v1";

    public static MappingPlanKey Create(
        IEnumerable<SourceField> sourceFields,
        string destinationFingerprint,
        string matcherVersion,
        SemanticMapperOptions options)
    {
        var builder = new StringBuilder()
            .Append(FormatVersion).Append('\n')
            .Append("matcher:").Append(matcherVersion).Append('\n')
            .Append("destination:").Append(destinationFingerprint).Append('\n')
            .Append(CultureInfo.InvariantCulture, $"policy:{options.MinimumConfidence:R}|{options.MinimumConfidenceGap:R}|{options.OnLowConfidence}|{options.OnAmbiguousMatch}")
            .Append('\n');

        foreach (var path in sourceFields.Select(f => f.IsArray ? f.Path + "[]" : f.Path).Order(StringComparer.Ordinal))
        {
            builder.Append("source:").Append(path).Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return new MappingPlanKey("smp1:" + Convert.ToHexString(hash).ToLowerInvariant());
    }
}
