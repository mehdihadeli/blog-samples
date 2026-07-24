namespace ECommerce.Products.Models;

// Entity model: sealed class, private ctor (for store materialization),
// private setters, static factory method — matches Catalogs conventions.
public sealed class Product
{
    internal Product() { }

    public Guid Id { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public int Stock { get; internal set; }

    // Application-managed concurrency token — incremented manually in TryUpdate.
    // Configured with .IsConcurrencyToken() in ECommerceDbContext.
    public int Version { get; internal set; }

    // Database auto-generated row version — PostgreSQL xmin system column.
    // EF Core + Npgsql maps .IsRowVersion() to the xmin column.
    // Updated automatically by PostgreSQL on every row modification.
    public uint RowVersion { get; set; }
    public decimal Price { get; internal set; }
    public DateTime CreatedAtUtc { get; internal set; }
    public DateTime UpdatedAtUtc { get; internal set; }

    public static Product Create(string name, int initialStock, decimal price)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Stock = initialStock,
            Price = price,
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
    }

    // Internal setters for store operations (not exposed to callers)
    public void DeductStock(int quantity)
    {
        Stock -= quantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
