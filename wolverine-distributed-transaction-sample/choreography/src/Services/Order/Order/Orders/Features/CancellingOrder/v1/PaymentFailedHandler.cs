using Contracts.Messages;
using Order.Orders.Models;
using Order.Shared.Data;

namespace Order.Orders.Features.CancellingOrder.v1;

/// <summary>
/// Handles payment failure — cancels the order (compensating action).
/// Idempotent: if order is already confirmed/cancelled, this is a no-op.
/// </summary>
public sealed class PaymentFailedHandler
{
    public async Task Handle(PaymentFailed failed, OrderDbContext dbContext)
    {
        var order = await dbContext.Orders.FindAsync(failed.OrderId);
        if (order is null || order.Status != OrderStatus.Pending)
            return;

        order.Cancel(failed.Reason);
        await dbContext.SaveChangesAsync();
    }
}
