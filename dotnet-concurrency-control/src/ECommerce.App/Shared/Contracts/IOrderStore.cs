using ECommerce.Orders.Models;

namespace ECommerce.Shared.Contracts;

// Abstraction over order persistence.
// Two implementations: InMemoryOrderStore (dev/demo) and
// EfOrderStore (PostgreSQL-backed in production).
public interface IOrderStore
{
    void Add(Order order);
    Order? Get(Guid id);
    IReadOnlyList<Order> GetAll();
}
