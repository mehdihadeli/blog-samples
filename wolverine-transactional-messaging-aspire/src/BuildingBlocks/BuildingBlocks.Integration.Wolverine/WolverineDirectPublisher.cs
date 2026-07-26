using BuildingBlocks.Abstractions.Messages;
using Wolverine;

namespace BuildingBlocks.Integration.Wolverine;

internal sealed class WolverineDirectPublisher(IMessageBus bus) : IBusDirectPublisher
{
    public ValueTask PublishAsync(IMessageEnvelope messageEnvelope, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var deliveryOptions = WolverineDeliveryOptionsFactory.TryBuild(messageEnvelope);

        return bus.PublishAsync(messageEnvelope.Message, deliveryOptions);
    }

    public ValueTask PublishAsync(
        IMessageEnvelope messageEnvelope,
        string? exchangeOrTopic,
        string? queue,
        CancellationToken ct = default
    )
    {
        ct.ThrowIfCancellationRequested();

        // Routing to specific exchange/topic is handled by Wolverine's
        // type-based topology configuration at startup.
        var deliveryOptions = WolverineDeliveryOptionsFactory.TryBuild(messageEnvelope);

        return bus.PublishAsync(messageEnvelope.Message, deliveryOptions);
    }
}
