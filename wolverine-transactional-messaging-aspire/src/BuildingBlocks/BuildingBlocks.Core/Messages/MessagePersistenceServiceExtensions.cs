namespace BuildingBlocks.Core.Messages;

/// <summary>
///     Convenience extension methods over <see cref="IMessagePersistenceService" />.
///     Automatically wraps raw messages in envelopes via <see cref="MessageEnvelopeFactory" />
///     before delegating to the persistence service.
///     Supported levels: 1 (Transactional Outbox via <c>PublishAsync</c>) +
///     3 (Durable Local Queue via <c>EnqueueLocalAsync</c>).
/// </summary>
public static class MessagePersistenceServiceExtensions
{
    public static ValueTask PublishAsync<TMessage>(
        this IMessagePersistenceService service,
        TMessage message,
        CancellationToken ct = default
    )
        where TMessage : class, IMessage
    {
        var envelope = MessageEnvelopeFactory.From(message);
        return service.PublishAsync(envelope, ct);
    }

    public static ValueTask EnqueueLocalAsync<TMessage>(
        this IMessagePersistenceService service,
        TMessage message,
        CancellationToken ct = default
    )
        where TMessage : class, IMessage
    {
        var envelope = MessageEnvelopeFactory.From(message);
        return service.EnqueueLocalAsync(envelope, ct);
    }
}
