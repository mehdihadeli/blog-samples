using BuildingBlocks.Core.Messages;
using Wolverine;

namespace BuildingBlocks.Integration.Wolverine;

internal static class WolverineDeliveryOptionsFactory
{
    internal static DeliveryOptions? TryBuild(IMessageEnvelope? envelope)
    {
        if (envelope is null)
        {
            return null;
        }

        return new DeliveryOptions { CorrelationId = envelope.Metadata.CorrelationId.ToString() }
            .WithHeader(MessageHeaders.MessageId, envelope.Metadata.MessageId.ToString())
            .WithHeader(MessageHeaders.CorrelationId, envelope.Metadata.CorrelationId.ToString())
            .WithHeader(MessageHeaders.Created, envelope.Metadata.Created.ToString("O"))
            .WithHeader(MessageHeaders.Type, envelope.Metadata.MessageType);
    }
}
