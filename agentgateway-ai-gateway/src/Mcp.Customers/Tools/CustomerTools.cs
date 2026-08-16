using ModelContextProtocol.Server;

namespace Mcp.Customers;

/// <summary>
/// Customer data tool server. Behind the gateway the tools are exposed as
/// <c>customers_*</c> and restricted to the support-admin role.
/// </summary>
[McpServerToolType]
public sealed class CustomerTools
{
    private static readonly List<Customer> Customers =
    [
        new("C-1", "Alice Example", "alice@example.com", "platinum"),
        new("C-2", "Bob Example", "bob@example.com", "gold"),
        new("C-3", "Carol Example", "carol@example.com", "standard"),
    ];

    /// <summary>
    /// Return the customer record for a customer id.
    /// </summary>
    [McpServerTool(Name = "customers_get")]
    public Customer? Get(string customerId)
    {
        return Customers.FirstOrDefault(c => c.Id == customerId);
    }

    /// <summary>
    /// Search customers by email or name.
    /// </summary>
    [McpServerTool(Name = "customers_search")]
    public IReadOnlyList<Customer> Search(string? email = null, string? name = null)
    {
        var results = Customers.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(email))
        {
            results = results.Where(c => c.Email.Contains(email, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            results = results.Where(c => c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        return results.ToList();
    }
}

/// <summary>
/// A customer record.
/// </summary>
public sealed class Customer
{
    public Customer(string id, string name, string email, string tier)
    {
        Id = id;
        Name = name;
        Email = email;
        Tier = tier;
    }

    public string Id { get; init; }
    public string Name { get; init; }
    public string Email { get; init; }
    public string Tier { get; init; }
}