using BuildingBlocks.Integration.Wolverine.Abstractions;
using Wolverine;

namespace BuildingBlocks.Integration.Wolverine;

internal sealed class WolverineDirectPublisher(IMessageBus bus) : IBusDirectPublisher
{
    public ValueTask PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deliveryOptions = WolverineDeliveryOptionsFactory.TryBuild(message);
        if (deliveryOptions is null)
        {
            return bus.PublishAsync(message);
        }

        return bus.PublishAsync(message, deliveryOptions);
    }
}
