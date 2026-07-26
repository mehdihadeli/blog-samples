namespace BuildingBlocks.Abstractions.Messages;

/// <summary>
///     External event bus — publishes integration events to the configured broker (RabbitMQ / Kafka).
///     Every call delegates to <see cref="IMessagePersistenceService" /> which enrolls the EF Core
///     DbContext in Wolverine's outbox, so messages are flushed only after the DB transaction commits.
///     Supported level: 1 (Transactional Outbox) — at-least-once delivery to the broker.
/// </summary>
public interface IExternalEventBus : IBusPublisher;
