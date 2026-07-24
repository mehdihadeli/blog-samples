using System.Collections.Concurrent;
using ECommerce.Orders.Models;
using ECommerce.Shared.Contracts;

namespace ECommerce.Shared.Data;

// In-memory order store — used when PostgreSQL is not configured.
internal sealed class InMemoryOrderStore : IOrderStore
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public void Add(Order order) => _orders[order.Id] = order;

    public Order? Get(Guid id) => _orders.TryGetValue(id, out var o) ? o : null;

    public IReadOnlyList<Order> GetAll() => _orders.Values.ToList();
}
