using Contracts.Messages;
using Order.Orders.Models;
using Order.Shared.Data;

namespace Order.Orders.Features.HandlingOrderTimeout.v1;

/// <summary>
/// Handles the timeout check — if order is still Pending, cancel it.
/// This is the timeout compensating action.
/// Idempotent: if order is already confirmed/cancelled, this is a no-op.
/// </summary>
public sealed class OrderTimeoutCheckHandler
{
    public async Task Handle(OrderTimeoutCheck timeout, OrderDbContext dbContext)
    {
        var order = await dbContext.Orders.FindAsync(timeout.OrderId);
        if (order is null || order.Status != OrderStatus.Pending)
            return; // Already completed — noop

        order.Cancel("timeout");
        await dbContext.SaveChangesAsync();
    }
}
