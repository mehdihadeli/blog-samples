using ECommerce.Products.Models;
using ECommerce.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Shared.Data;

// EF Core + PostgreSQL implementation of IProductStore.
// Uses IDbContextFactory so it can be registered as singleton
// — each operation creates & disposes its own short-lived context.
public sealed class EfProductStore : IProductStore
{
    private readonly IDbContextFactory<ECommerceDbContext> _contextFactory;

    public EfProductStore(IDbContextFactory<ECommerceDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public Product Get(Guid id)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Products.AsNoTracking().First(p => p.Id == id);
    }

    public IReadOnlyList<Product> GetAll()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Products.AsNoTracking().ToList();
    }

    public bool Exists(Guid id)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Products.Any(p => p.Id == id);
    }

    // Optimistic write: loads tracked entity, applies transform, saves.
    // If Version changed concurrently, EF Core throws
    // DbUpdateConcurrencyException → we return false + fresh state.
    public (bool success, Product? product, int storeVersion) TryUpdate(
        Guid id,
        int expectedVersion,
        Func<Product, Product> transform
    )
    {
        using var context = _contextFactory.CreateDbContext();

        var product = context.Products.FirstOrDefault(p => p.Id == id);
        if (product is null)
            return (false, null, 0);

        if (product.Version != expectedVersion)
            return (false, Clone(product), product.Version);

        transform(product);
        product.Version = expectedVersion + 1;
        product.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            context.SaveChanges();
            return (true, Clone(product), product.Version);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Reload to get current DB state for caller feedback
            context.Entry(product).Reload();
            return (false, Clone(product), product.Version);
        }
    }

    // Force-write used by NoLock / LocalLock strategies.
    // Still respects concurrency token — if conflict, the exception
    // propagates up (those strategies don't retry).
    public void Write(Product product)
    {
        using var context = _contextFactory.CreateDbContext();

        var tracked = context.Products.FirstOrDefault(p => p.Id == product.Id);
        if (tracked is not null)
        {
            context.Entry(tracked).CurrentValues.SetValues(product);
            tracked.Version++;
            tracked.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            context.Products.Add(product);
        }

        context.SaveChanges();
    }

    public void Seed(Product product)
    {
        using var context = _contextFactory.CreateDbContext();
        context.Products.Add(product);
        context.SaveChanges();
    }

    private static Product Clone(Product p) =>
        new()
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
