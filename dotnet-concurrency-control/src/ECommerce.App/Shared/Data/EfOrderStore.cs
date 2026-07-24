using ECommerce.Orders.Models;
using ECommerce.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Shared.Data;

// EF Core + PostgreSQL implementation of IOrderStore.
// Same singleton + IDbContextFactory pattern as EfProductStore.
public sealed class EfOrderStore : IOrderStore
{
    private readonly IDbContextFactory<ECommerceDbContext> _contextFactory;

    public EfOrderStore(IDbContextFactory<ECommerceDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public void Add(Order order)
    {
        using var context = _contextFactory.CreateDbContext();
        context.Orders.Add(order);
        context.SaveChanges();
    }

    public Order? Get(Guid id)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Orders.AsNoTracking().FirstOrDefault(o => o.Id == id);
    }

    public IReadOnlyList<Order> GetAll()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Orders.AsNoTracking().ToList();
    }
}
