namespace BuildingBlocks.Integration.Wolverine.Abstractions;

public interface IExternalEventBus
{
    ValueTask PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class;
}
