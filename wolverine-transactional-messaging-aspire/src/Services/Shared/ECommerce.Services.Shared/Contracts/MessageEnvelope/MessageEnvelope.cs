using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.Wolverine.Abstractions;

namespace ECommerce.Services.Shared.Contracts.MessageEnvelope;

public sealed record MessageEnvelope<TMessage>(
    Guid MessageId,
    Guid CorrelationId,
    DateTime OccurredAtUtc,
    TMessage Message
) : IWolverineMessageEnvelope
    where TMessage : IMessage;

public static class MessageEnvelope
{
    public static MessageEnvelope<TMessage> Create<TMessage>(
        TMessage message,
        Guid? correlationId = null,
        Guid? messageId = null,
        DateTime? occurredAtUtc = null
    )
        where TMessage : IMessage
    {
        return new MessageEnvelope<TMessage>(
            messageId ?? Guid.NewGuid(),
            correlationId ?? Guid.NewGuid(),
            occurredAtUtc ?? DateTime.UtcNow,
            message
        );
    }
}
