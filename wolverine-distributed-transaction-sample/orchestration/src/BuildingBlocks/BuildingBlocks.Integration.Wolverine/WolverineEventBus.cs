using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.Wolverine.Abstractions;
using Wolverine;

namespace BuildingBlocks.Integration.Wolverine;

internal sealed class WolverineEventBus(IMessageBus messageBus) : IEventBus
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
        => messageBus.PublishAsync(message).AsTask();
}
