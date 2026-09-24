namespace SemanticMapper.Core.Tests;

public class Customer
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public string NationalId { get; set; } = "";
}

public class Address
{
    public string? Street { get; set; }
    public string? City { get; set; }
}

public class CustomerWithAddress
{
    public string? FirstName { get; set; }
    public Address? Address { get; set; }
}

public enum AccountStatus
{
    Active,
    Suspended,
}

public class AllTypes
{
    public string? Text { get; set; }
    public int Count { get; set; }
    public long Big { get; set; }
    public decimal Amount { get; set; }
    public double Ratio { get; set; }
    public bool IsActive { get; set; }
    public DateTime When { get; set; }
    public DateTimeOffset WhenOffset { get; set; }
    public DateOnly Day { get; set; }
    public TimeOnly At { get; set; }
    public TimeSpan Duration { get; set; }
    public Guid Id { get; set; }
    public Uri? Website { get; set; }
    public AccountStatus Status { get; set; }
    public int? MaybeCount { get; set; }
    public DateTime? MaybeWhen { get; set; }
    public List<string> Tags { get; set; } = [];
    public int[] Scores { get; set; } = [];
    public IReadOnlyList<Guid>? Ids { get; set; }
}

public class Node
{
    public string? Name { get; set; }
    public Node? Parent { get; set; }
}

public class WithUnsettable
{
    public string? Settable { get; set; }
    public string? InitOnly { get; init; }
    public string Computed => Settable ?? "";
    public string? PrivateSet { get; private set; }
}

public abstract class AbstractModel
{
    public string? Name { get; set; }
}

public class NoDefaultConstructor(string name)
{
    public string Name { get; set; } = name;
}

public class NoProperties
{
}
