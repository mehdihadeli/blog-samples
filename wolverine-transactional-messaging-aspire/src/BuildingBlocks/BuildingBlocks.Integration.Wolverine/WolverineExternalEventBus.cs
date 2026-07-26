using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Abstractions.Types;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Integration.Wolverine;

internal sealed class WolverineExternalEventBus(IServiceProvider serviceProvider)
    : IExternalEventBus
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken ct = default)
        where TMessage : class, IMessage
    {
        ct.ThrowIfCancellationRequested();

        var metadataAccessor = serviceProvider.GetRequiredService<IMessageMetadataAccessor>();
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

        var persistenceService = serviceProvider.GetRequiredService<IMessagePersistenceService>();
        return persistenceService.PublishAsync(envelope, ct).AsTask();
    }

    public Task PublishAsync(IMessageEnvelope messageEnvelope, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var persistenceService = serviceProvider.GetRequiredService<IMessagePersistenceService>();
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
        ct.ThrowIfCancellationRequested();

        var metadataAccessor = serviceProvider.GetRequiredService<IMessageMetadataAccessor>();
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

        var persistenceService = serviceProvider.GetRequiredService<IMessagePersistenceService>();
        return persistenceService.PublishAsync(envelope, ct).AsTask();
    }

    public Task PublishAsync(
        IMessageEnvelope messageEnvelope,
        string? exchangeOrTopic,
        string? queue,
        CancellationToken ct = default
    )
    {
        ct.ThrowIfCancellationRequested();

        var persistenceService = serviceProvider.GetRequiredService<IMessagePersistenceService>();
        return persistenceService.PublishAsync(messageEnvelope, ct).AsTask();
    }
}
