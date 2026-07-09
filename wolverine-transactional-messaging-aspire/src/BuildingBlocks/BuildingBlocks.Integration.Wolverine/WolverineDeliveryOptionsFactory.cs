using BuildingBlocks.Integration.Wolverine.Abstractions;
using Wolverine;

namespace BuildingBlocks.Integration.Wolverine;

internal static class WolverineDeliveryOptionsFactory
{
    internal static DeliveryOptions? TryBuild<TMessage>(TMessage message)
        where TMessage : class
    {
        if (message is not IWolverineMessageEnvelope envelope)
        {
            return null;
        }

        return new DeliveryOptions { CorrelationId = envelope.CorrelationId.ToString() }
            .WithHeader("message-id", envelope.MessageId.ToString())
            .WithHeader("correlation-id", envelope.CorrelationId.ToString())
            .WithHeader("occurred-at-utc", envelope.OccurredAtUtc.ToString("O"))
            .WithHeader("message-type", typeof(TMessage).FullName ?? typeof(TMessage).Name);
    }
}
