namespace BuildingBlocks.Integration.MassTransit.Abstractions;

public interface IMassTransitMessagePublisher
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class;
}
