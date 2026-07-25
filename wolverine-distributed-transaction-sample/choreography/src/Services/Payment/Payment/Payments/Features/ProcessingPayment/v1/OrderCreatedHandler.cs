using Contracts.Messages;
using Wolverine;

namespace Payment.Payments.Features.ProcessingPayment.v1;

/// <summary>
/// Handles OrderCreated events from the Order service.
/// Simulates async payment gateway call — 80% success, 500ms delay.
/// In choreography, the Payment service reacts to events independently,
/// without any central saga coordinator telling it what to do.
/// </summary>
public sealed class OrderCreatedHandler
{
    private static readonly Random _random = new();

    public async Task Handle(OrderCreated created, IMessageBus bus)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        if (_random.NextDouble() < 0.8)
        {
            await bus.PublishAsync(
                new PaymentProcessed(created.OrderId, $"TXN-{Guid.NewGuid():N}"[..12])
            );
        }
        else
        {
            await bus.PublishAsync(new PaymentFailed(created.OrderId, "Insufficient funds"));
        }
    }
}
