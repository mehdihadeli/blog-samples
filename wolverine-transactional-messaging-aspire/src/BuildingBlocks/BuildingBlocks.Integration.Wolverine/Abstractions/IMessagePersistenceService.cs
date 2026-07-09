namespace BuildingBlocks.Integration.Wolverine.Abstractions;

public interface IMessagePersistenceService
{
    ValueTask PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class;

    ValueTask EnqueueLocalAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class;
}
