using BuildingBlocks.Core.Messages;
using BuildingBlocks.Core.Types;

namespace BuildingBlocks.Integration.Wolverine;

internal sealed class WolverineExternalEventBus(
    IMessageMetadataAccessor metadataAccessor,
    IMessagePersistenceService persistenceService
) : IExternalEventBus
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken ct = default)
        where TMessage : class, IMessage
    {
        var correlationId = metadataAccessor.GetCorrelationId();
        var messageTypeName = message.GetType().Name;

        var envelope = MessageEnvelopeFactory.From(
            message,
            correlationId,
            message.MessageId,
            new Dictionary<string, object?>
            {
                { MessageHeaders.Name, messageTypeName },
                { MessageHeaders.Type, TypeMapper.GetShortTypeName<TMessage>() },
            }
        );

        return persistenceService.PublishAsync(envelope, ct).AsTask();
    }

    public Task PublishAsync(IMessageEnvelope messageEnvelope, CancellationToken ct = default)
    {
        return persistenceService.PublishAsync(messageEnvelope, ct).AsTask();
    }

    public Task PublishAsync<TMessage>(
        TMessage message,
        string? exchangeOrTopic,
        string? queue,
        CancellationToken ct = default
    )
        where TMessage : class, IMessage
    {
        var correlationId = metadataAccessor.GetCorrelationId();
        var messageTypeName = message.GetType().Name;

        var envelope = MessageEnvelopeFactory.From(
            message,
            correlationId,
            message.MessageId,
            new Dictionary<string, object?>
            {
                { MessageHeaders.Name, messageTypeName },
                { MessageHeaders.Type, TypeMapper.GetShortTypeName<TMessage>() },
                { MessageHeaders.ExchangeOrTopic, exchangeOrTopic ?? messageTypeName },
                { MessageHeaders.Queue, queue ?? messageTypeName },
            }
        );

        return persistenceService.PublishAsync(envelope, ct).AsTask();
    }

    public Task PublishAsync(
        IMessageEnvelope messageEnvelope,
        string? exchangeOrTopic,
        string? queue,
        CancellationToken ct = default
    )
    {
        return persistenceService.PublishAsync(messageEnvelope, ct).AsTask();
    }
}
