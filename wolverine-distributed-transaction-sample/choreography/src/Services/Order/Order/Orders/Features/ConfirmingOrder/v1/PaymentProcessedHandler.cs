using Contracts.Messages;
using Order.Orders.Models;
using Order.Shared.Data;

namespace Order.Orders.Features.ConfirmingOrder.v1;

/// <summary>
/// Handles successful payment — confirms the order.
/// This is the happy path forward action (not a compensation).
/// Idempotent: if order is already confirmed/cancelled, this is a no-op.
/// </summary>
public sealed class PaymentProcessedHandler
{
    public async Task Handle(PaymentProcessed processed, OrderDbContext dbContext)
    {
        var order = await dbContext.Orders.FindAsync(processed.OrderId);
        if (order is null || order.Status != OrderStatus.Pending)
            return; // Idempotent — already processed or cancelled

        order.Confirm();
        await dbContext.SaveChangesAsync();
    }
}
