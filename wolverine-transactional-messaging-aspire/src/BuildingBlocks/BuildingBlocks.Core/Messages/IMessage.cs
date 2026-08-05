namespace BuildingBlocks.Core.Messages;

/// <summary>
///     Base message contract. Every message carries a unique <see cref="MessageId" />
///     and <see cref="Created" /> timestamp.
///     Supported levels: core/utility — foundation for all messaging (Levels 1-5).
/// </summary>
public interface IMessage
{
    Guid MessageId { get; }
    DateTime Created { get; }
}

/// <summary>
///     Cross-service integration event marker.
///     Supported level: 1 (Transactional Outbox) + 2 (Durable Inbox) — published to broker, consumed by other services.
/// </summary>
public interface IIntegrationEvent : IMessage;

/// <summary>
///     Same-service internal command marker.
///     Supported levels: 3 (Durable Local Queue) + 4 (Background Job Scheduler) — processed in-process, never leaves the service.
/// </summary>
public interface IInternalCommand : IMessage;
