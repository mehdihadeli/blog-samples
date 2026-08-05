using BuildingBlocks.Core.Messages;
using BuildingBlocks.Integration.Wolverine.Kafka;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.Messaging;

namespace ECommerce.Services.Orders;

/// <summary>
/// Orders' Kafka manual consume topology (OPTION 1).
/// Used when <c>AutoConfigMessagesTopology = false</c> — passed as
/// the <c>configure</c> callback to <c>AddWolverineKafka</c>.
///
/// OPTION 2 (auto-scan) is handled by
/// <c>ApplyMessagesConsumeTopology</c> in the building blocks layer —
/// scans assemblies for <c>IIntegrationEvent</c> types automatically.
/// </summary>
public static class WolverineKafkaOrdersTopologyExtensions
{
    /// <summary>
    /// Explicit per-message-type consumer listeners with snake_case naming.
    /// Consumes <c>ProductCreatedV1</c> and <c>OrderSubmittedV1</c>.
    /// </summary>
    public static WolverineKafkaRegistrationBuilder ConfigureOrdersConsumeTopology(
        this WolverineKafkaRegistrationBuilder builder
    )
    {
        // OPTION 1 (manual): Explicit per-message-type listeners.
        // Transparent, debuggable — repeat per consumed type.
        // Topic auto-derived via snake_case; consumer group explicit.

        builder.UseSnakeCaseConventions();

        builder.Listen<MessageEnvelope<ProductCreatedV1>>(
            topicName: null, // auto-derived: product_created_v1
            MessagingConstants.OrdersProductsConsumerGroup
        );

        builder.Listen<MessageEnvelope<OrderSubmittedV1>>(
            topicName: null, // auto-derived: order_submitted_v1
            MessagingConstants.OrdersOrdersConsumerGroup
        );

        return builder;
    }
}
