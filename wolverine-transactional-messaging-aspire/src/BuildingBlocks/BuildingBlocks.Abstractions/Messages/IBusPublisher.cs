namespace BuildingBlocks.Abstractions.Messages;

/// <summary>
///     Base outbound publisher contract inherited by <see cref="IExternalEventBus" />.
///     Accepts both raw messages (auto-wrapped into envelopes) and pre-built <see cref="IMessageEnvelope" /> instances.
///     Supported levels: core/utility — base contract for Level 1 (Transactional Outbox).
/// </summary>
public interface IBusPublisher
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken ct = default)
        where TMessage : class, IMessage;

    Task PublishAsync(IMessageEnvelope messageEnvelope, CancellationToken ct = default);

    Task PublishAsync<TMessage>(
        TMessage message,
        string? exchangeOrTopic,
        string? queue,
        CancellationToken ct = default
    )
        where TMessage : class, IMessage;

    Task PublishAsync(
        IMessageEnvelope messageEnvelope,
        string? exchangeOrTopic,
        string? queue,
        CancellationToken ct = default
    );
}
