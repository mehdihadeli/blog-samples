// ── DEPRECATED ─────────────────────────────────────────────
// OrderStore has been moved to ECommerce.Shared.Contracts.IOrderStore.
// This file is kept to avoid breaking the build on open files;
// remove it after confirming nothing references this type.
// ───────────────────────────────────────────────────────────

using ECommerce.Shared.Contracts;

namespace ECommerce.Orders.Models;

// Kept for binary compat — use IOrderStore from Shared/Contracts/.
public abstract class OrderStore : IOrderStore
{
    public abstract void Add(Order order);
    public abstract Order? Get(Guid id);
    public abstract IReadOnlyList<Order> GetAll();
}
