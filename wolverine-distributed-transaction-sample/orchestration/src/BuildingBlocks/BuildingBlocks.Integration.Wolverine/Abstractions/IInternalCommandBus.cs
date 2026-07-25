using BuildingBlocks.Abstractions.Messages;

namespace BuildingBlocks.Integration.Wolverine.Abstractions;

public interface IInternalCommandBus
{
    Task EnqueueAsync<T>(T command, CancellationToken cancellationToken = default)
        where T : class, IInternalCommand;
}
