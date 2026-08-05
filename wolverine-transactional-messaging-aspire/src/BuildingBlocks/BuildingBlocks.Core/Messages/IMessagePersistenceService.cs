namespace BuildingBlocks.Core.Messages;

/// <summary>
///     Outbox / persistence coordinator. Enrolls the current EF Core DbContext in Wolverine's
///     <c>IDbContextOutbox</c>, then delegates to <see cref="IBusDirectPublisher" /> or local queue.
///     Supported levels: 1 (Transactional Outbox via <see cref="PublishAsync" />) +
///     3 (Durable Local Queue via <see cref="EnqueueLocalAsync" />).
/// </summary>
public interface IMessagePersistenceService
{
    ValueTask PublishAsync(IMessageEnvelope messageEnvelope, CancellationToken ct = default);
    ValueTask EnqueueLocalAsync(IMessageEnvelope messageEnvelope, CancellationToken ct = default);
}
