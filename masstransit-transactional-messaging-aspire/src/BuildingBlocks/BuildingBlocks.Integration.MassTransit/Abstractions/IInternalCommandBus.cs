using BuildingBlocks.Abstractions.Messages;

namespace BuildingBlocks.Integration.MassTransit.Abstractions;

/// <summary>
/// Bus for dispatching internal commands within the same service boundary.
/// Implementations may use in-memory mediator (non-durable) or
/// durable local queues (Wolverine-style) for restart-safe processing.
/// </summary>
public interface IInternalCommandBus
{
    /// <summary>
    /// Enqueues an internal command for processing. The command must implement
    /// <see cref="IInternalCommand"/>.
    /// </summary>
    Task EnqueueAsync<T>(T command, CancellationToken cancellationToken = default)
        where T : class, IInternalCommand;
}
