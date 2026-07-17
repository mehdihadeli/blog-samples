using BuildingBlocks.Integration.MassTransit.Abstractions;

namespace BuildingBlocks.Integration.MassTransit;

internal sealed class MassTransitEventBus(IMassTransitMessagePublisher messagePublisher) : IEventBus
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        return messagePublisher.PublishAsync(message, cancellationToken);
    }
}
