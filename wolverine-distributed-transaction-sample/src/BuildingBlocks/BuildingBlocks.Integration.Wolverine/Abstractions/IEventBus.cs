namespace BuildingBlocks.Integration.Wolverine.Abstractions;

public interface IEventBus
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class;
}
