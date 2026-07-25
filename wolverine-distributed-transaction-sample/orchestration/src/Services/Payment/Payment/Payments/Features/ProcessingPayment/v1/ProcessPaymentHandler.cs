using Contracts.Messages;
using Wolverine;

namespace Payment.Payments.Features.ProcessingPayment.v1;

/// <summary>
/// Handles incoming ProcessPayment requests from OrderSaga.
/// Simulates async payment gateway call — 80% success, 500ms delay.
/// </summary>
public sealed class ProcessPaymentHandler
{
    private static readonly Random _random = new();

    public async Task Handle(ProcessPayment command, IMessageBus bus)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        if (_random.NextDouble() < 0.8)
        {
            await bus.PublishAsync(new PaymentProcessed(
                command.OrderId,
                $"TXN-{Guid.NewGuid():N}"[..12]));
        }
        else
        {
            await bus.PublishAsync(new PaymentFailed(
                command.OrderId,
                "Insufficient funds"));
        }
    }
}
