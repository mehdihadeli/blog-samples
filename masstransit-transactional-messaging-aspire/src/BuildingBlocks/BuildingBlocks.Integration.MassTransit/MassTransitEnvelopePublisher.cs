using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.MassTransit.Abstractions;
using MassTransit;

namespace BuildingBlocks.Integration.MassTransit;

internal sealed class MassTransitEnvelopePublisher(IPublishEndpoint publishEndpoint)
    : IMassTransitMessagePublisher
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (message is IMessageEnvelopeMetadata envelope)
        {
            return publishEndpoint.Publish(
                message,
                context =>
                {
                    context.MessageId = envelope.MessageId;
                    context.CorrelationId = envelope.CorrelationId;
                    context.Headers.Set("occurred-at-utc", envelope.OccurredAtUtc.ToString("O"));
                    context.Headers.Set(
                        "message-type",
                        envelope.MessageType.FullName ?? envelope.MessageType.Name
                    );
                },
                cancellationToken
            );
        }

        return publishEndpoint.Publish(message, cancellationToken);
    }
}
