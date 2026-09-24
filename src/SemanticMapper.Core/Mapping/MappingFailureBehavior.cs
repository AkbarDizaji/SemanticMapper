namespace SemanticMapper;

/// <summary>
/// What the mapper does when a match does not satisfy the confidence rules.
/// </summary>
public enum MappingFailureBehavior
{
    /// <summary>Fail the mapping with an <see cref="UnsafeMappingException"/>.</summary>
    Throw = 0,

    /// <summary>Leave the destination property at its default value.</summary>
    LeaveDefault,

    /// <summary>Use the highest-scoring available candidate even though it does not satisfy the confidence rules.</summary>
    UseBestMatch,
}
