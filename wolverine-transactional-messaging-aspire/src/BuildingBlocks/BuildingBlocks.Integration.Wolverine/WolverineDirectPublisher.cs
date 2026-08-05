using BuildingBlocks.Core.Messages;
using Wolverine;

namespace BuildingBlocks.Integration.Wolverine;

internal sealed class WolverineDirectPublisher(IMessageBus bus) : IBusDirectPublisher
{
    public ValueTask PublishAsync(IMessageEnvelope messageEnvelope, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var deliveryOptions = WolverineDeliveryOptionsFactory.TryBuild(messageEnvelope);

        // Publish the ENVELOPE, not the raw inner message. RabbitMQ topology
        // (auto-config or manual) registers routes for MessageEnvelope<T>, so
        // publishing the raw inner type would fall back to conventional routing
        // and land on the plain exchange with an EMPTY routing key — silently
        // dropped on a Topic exchange. TrackActivity also records Sent only for
        // the type actually published (the wrapper), so tests asserting
        // MessageEnvelope<T> would see nothing.
        return bus.PublishAsync(messageEnvelope, deliveryOptions);
    }

    public ValueTask PublishAsync(
        IMessageEnvelope messageEnvelope,
        string? exchangeOrTopic,
        string? queue,
        CancellationToken ct = default
    )
    {
        ct.ThrowIfCancellationRequested();

        // Routing to a specific exchange / topic is handled by Wolverine's
        // type-based topology configuration at startup.
        var deliveryOptions = WolverineDeliveryOptionsFactory.TryBuild(messageEnvelope);

        // Same envelope-first publish as above — the raw inner type has no
        // registered route (topology only knows MessageEnvelope<T>).
        return bus.PublishAsync(messageEnvelope, deliveryOptions);
    }
}
