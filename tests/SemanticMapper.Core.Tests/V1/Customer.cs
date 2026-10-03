// Version 1 of the destination model, paired with V2.Customer to verify plan-cache
// invalidation when the destination schema changes.
namespace SemanticMapper.Core.Tests.V1;

public class Customer
{
    public string? FirstName { get; set; }
}
