using BuildingBlocks.Integration.Wolverine.Abstractions;

namespace BuildingBlocks.Integration.Wolverine;

internal sealed class WolverineExternalEventBus(
    IMessagePersistenceService messagePersistenceService
) : IExternalEventBus
{
    public ValueTask PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class
    {
        return messagePersistenceService.PublishAsync(message, cancellationToken);
    }
}
