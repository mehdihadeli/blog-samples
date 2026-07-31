using BuildingBlocks.Core.Messages;
using ECommerce.BuildingBlocks.TestHost.Messages;

namespace ECommerce.BuildingBlocks.TestHost.Handlers;

// In-process handlers so TrackActivity's external-transport tracking can
// complete: with IncludeExternalTransports() the Sent record of an external
// (broker) message only completes when the matching Received arrives.
//
// NOTE: Wolverine's conventional handler discovery only picks up public
// concrete types whose name ends with the singular suffix "Handler" or
// "Consumer" (HandlerQuery.Includes.WithNameSuffix("Handler")). A class
// named e.g. "TestMessageHandlers" (plural) is silently ignored and the
// host logs "Wolverine found no handlers." — so keep the singular suffix.
public static class ProductCreatedV1Handler
{
    public static Task Handle(
        MessageEnvelope<ProductCreatedV1> envelope,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;
}

public static class OrderCreatedV1Handler
{
    public static Task Handle(
        MessageEnvelope<OrderCreatedV1> envelope,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;
}

public static class InventoryAdjustedV1Handler
{
    public static Task Handle(
        MessageEnvelope<InventoryAdjustedV1> envelope,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;
}
