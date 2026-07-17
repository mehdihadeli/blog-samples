using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.MassTransit.Abstractions;
using MassTransit.Mediator;

namespace BuildingBlocks.Integration.MassTransit;

/// <summary>
/// Non-durable internal command bus backed by MassTransit Mediator.
/// Commands are dispatched in-memory after the current transaction commits.
/// If the process crashes before processing, the command is lost.
/// For restart-safe processing, use the durable local queue implementation instead.
/// </summary>
internal sealed class MassTransitInternalCommandBus(IMediator mediator) : IInternalCommandBus
{
    public Task EnqueueAsync<T>(T command, CancellationToken cancellationToken = default)
        where T : class, IInternalCommand => mediator.Publish(command, cancellationToken);
}
