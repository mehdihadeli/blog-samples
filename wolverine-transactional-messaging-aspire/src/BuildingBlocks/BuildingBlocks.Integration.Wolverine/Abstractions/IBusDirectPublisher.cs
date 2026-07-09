namespace BuildingBlocks.Integration.Wolverine.Abstractions;

public interface IBusDirectPublisher
{
    ValueTask PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class;
}
