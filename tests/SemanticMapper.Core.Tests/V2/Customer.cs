// Version 2 of the destination model, paired with V1.Customer to verify plan-cache
// invalidation when the destination schema changes.
namespace SemanticMapper.Core.Tests.V2;

public class Customer
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
