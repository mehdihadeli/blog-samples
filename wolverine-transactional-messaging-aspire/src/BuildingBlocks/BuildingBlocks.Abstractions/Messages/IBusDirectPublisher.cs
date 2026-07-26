namespace BuildingBlocks.Abstractions.Messages;

/// <summary>
///     Low-level transport publisher. Bypasses the outbox — publishes directly to the broker.
///     Used internally by <see cref="IMessagePersistenceService" />.
///     Accepts only envelopes — wrapping is the caller's responsibility.
///     Supported level: 1 (Transactional Outbox) — transport layer inside the outbox pipeline.
/// </summary>
public interface IBusDirectPublisher
{
    ValueTask PublishAsync(IMessageEnvelope messageEnvelope, CancellationToken ct = default);

    ValueTask PublishAsync(
        IMessageEnvelope messageEnvelope,
        string? exchangeOrTopic,
        string? queue,
        CancellationToken ct = default
    );
}
