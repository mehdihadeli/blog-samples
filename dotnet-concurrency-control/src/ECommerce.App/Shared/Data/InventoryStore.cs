using ECommerce.Products.Models;
using ECommerce.Shared.Contracts;

namespace ECommerce.Shared.Data;

// In-memory implementation of IProductStore.
// In production, this would be EF Core + PostgreSQL (like CatalogsDbContext).
public sealed class InventoryStore : IProductStore
{
    private readonly Dictionary<Guid, Product> _products = new();
    private readonly object _lock = new();

    public Product Get(Guid id)
    {
        lock (_lock)
        {
            // Return a copy to prevent external mutation
            var original = _products[id];
            return Clone(original);
        }
    }

    public IReadOnlyList<Product> GetAll()
    {
        lock (_lock)
        {
            return _products.Values.Select(Clone).ToList().AsReadOnly();
        }
    }

    public bool Exists(Guid id)
    {
        lock (_lock)
        {
            return _products.ContainsKey(id);
        }
    }

    // Optimistic write: only succeeds if the version matches.
    public (bool success, Product? product, int storeVersion) TryUpdate(
        Guid id,
        int expectedVersion,
        Func<Product, Product> transform
    )
    {
        lock (_lock)
        {
            if (!_products.TryGetValue(id, out var current))
                return (false, null, 0);

            if (current.Version != expectedVersion)
                return (false, Clone(current), current.Version);

            var updated = transform(Clone(current));
            updated.Version = current.Version + 1;
            updated.UpdatedAtUtc = DateTime.UtcNow;
            _products[id] = updated;
            return (true, Clone(updated), updated.Version);
        }
    }

    // Force write (used by locking strategies that bypass optimistic checks).
    public void Write(Product product)
    {
        lock (_lock)
        {
            product.Version++;
            product.UpdatedAtUtc = DateTime.UtcNow;
            _products[product.Id] = product;
        }
    }

    public void Seed(Product product)
    {
        lock (_lock)
        {
            _products[product.Id] = product;
        }
    }

    private static Product Clone(Product p)
    {
        // Workaround: since Product has private setters, use the internal state.
        // In EF Core this is handled by change tracking.
        var clone = new Product();
        // Use reflection-free approach: write a new product from scratch.
        // For demo purposes we access via a simple workaround.
        return new Product
        {
            Id = p.Id,
            Name = p.Name,
            Stock = p.Stock,
            Version = p.Version,
            Price = p.Price,
            CreatedAtUtc = p.CreatedAtUtc,
            UpdatedAtUtc = p.UpdatedAtUtc,
        };
    }
}
