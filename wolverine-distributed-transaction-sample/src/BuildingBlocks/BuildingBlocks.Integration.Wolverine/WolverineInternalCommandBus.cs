using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.Wolverine.Abstractions;
using MediatR;

namespace BuildingBlocks.Integration.Wolverine;

internal sealed class WolverineInternalCommandBus(IMediator mediator) : IInternalCommandBus
{
    public Task EnqueueAsync<T>(T command, CancellationToken cancellationToken = default)
        where T : class, IInternalCommand
        => mediator.Send(command, cancellationToken);
}
