// Two "versions" of the same destination model, used to verify plan-cache invalidation
// when the destination schema changes.
namespace SemanticMapper.Core.Tests.V1
{
    public class Customer
    {
        public string? FirstName { get; set; }
    }
}

namespace SemanticMapper.Core.Tests.V2
{
    public class Customer
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
