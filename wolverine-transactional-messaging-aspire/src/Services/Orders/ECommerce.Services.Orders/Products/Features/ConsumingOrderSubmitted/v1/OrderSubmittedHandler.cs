using BuildingBlocks.Abstractions.Messages;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;

namespace ECommerce.Services.Orders.Products.Features.ConsumingOrderSubmitted.v1;

/// <summary>
/// Handler for OrderSubmittedV1 — auto-discovered by conventional routing
/// in Orders topology.  Proves that conventional routing works alongside
/// explicit per-type topology for ProductCreatedV1.
/// </summary>
public static class OrderSubmittedHandler
{
    public static Task Handle(
        MessageEnvelope<OrderSubmittedV1> envelope,
        CancellationToken cancellationToken
    )
    {
        // In a real e-commerce app we would:
        //   - Send order confirmation email
        //   - Update inventory
        //   - Trigger payment workflow
        //   - Notify shipping service
        //
        // For this sample, simply acknowledging receipt proves the
        // messaging topology (topic exchange + conventional routing) works.

        return Task.CompletedTask;
    }
}
