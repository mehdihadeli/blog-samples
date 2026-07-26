using BuildingBlocks.Abstractions.Types;

namespace BuildingBlocks.Abstractions.Messages;

/// <summary>
///     Static factory for building <see cref="MessageEnvelope{T}" /> from raw messages.
///     Auto-populates <see cref="MessageEnvelopeMetadata" /> with type names, correlation ID, causation ID, and headers.
///     Supported levels: core/utility — used by Levels 1 (Outbox), 3 (Local Queue), and 4 (Scheduler) for envelope construction.
/// </summary>
public static class MessageEnvelopeFactory
{
    public static MessageEnvelope<T> From<T>(T data)
        where T : class, IMessage
    {
        var typeName = data.GetType().Name;
        var envelopeMetadata = new MessageEnvelopeMetadata(
            data.MessageId,
            Guid.NewGuid(),
            TypeMapper.GetShortTypeName<T>(),
            typeName,
            data.MessageId
        )
        {
            Headers = new Dictionary<string, object?>
            {
                { MessageHeaders.Name, typeName },
                { MessageHeaders.Type, TypeMapper.GetShortTypeName<T>() },
            },
        };

        return new MessageEnvelope<T>(data, envelopeMetadata);
    }

    public static MessageEnvelope<T> From<T>(T data, MessageEnvelopeMetadata metadata)
        where T : class, IMessage
    {
        return new MessageEnvelope<T>(data, metadata);
    }

    public static IMessageEnvelope From(object data, MessageEnvelopeMetadata metadata)
    {
        var methodInfo = typeof(MessageEnvelopeFactory)
            .GetMethods()
            .FirstOrDefault(x =>
                string.Equals(x.Name, nameof(From), StringComparison.OrdinalIgnoreCase)
                && x.GetGenericArguments().Length != 0
                && x.GetParameters().Length == 2
            );

        var genericMethod = methodInfo!.MakeGenericMethod(data.GetType());
        return (IMessageEnvelope)genericMethod.Invoke(null, [data, metadata])!;
    }

    public static MessageEnvelope<T> From<T>(
        T data,
        Guid correlationId,
        Guid? causationId = null,
        IDictionary<string, object?>? headers = null
    )
        where T : class, IMessage
    {
        var envelopeMetadata = new MessageEnvelopeMetadata(
            data.MessageId,
            correlationId,
            TypeMapper.GetShortTypeName<T>(),
            data.GetType().Name,
            causationId
        )
        {
            Headers = headers ?? new Dictionary<string, object?>(),
        };

        return new MessageEnvelope<T>(data, envelopeMetadata);
    }

    public static IMessageEnvelope From(
        object data,
        Guid correlationId,
        Guid? causationId = null,
        IDictionary<string, object?>? headers = null
    )
    {
        var methodInfo = typeof(MessageEnvelopeFactory)
            .GetMethods()
            .FirstOrDefault(x =>
                string.Equals(x.Name, nameof(From), StringComparison.OrdinalIgnoreCase)
                && x.GetGenericArguments().Length != 0
                && x.GetParameters().Length == 4
            );

        var genericMethod = methodInfo!.MakeGenericMethod(data.GetType());
        return (IMessageEnvelope)
            genericMethod.Invoke(null, [data, correlationId, causationId, headers])!;
    }
}
