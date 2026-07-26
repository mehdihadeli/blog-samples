using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.Wolverine.Kafka;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.Messaging;

namespace ECommerce.Services.Orders;

/// <summary>
/// Orders' Kafka topology — consumes integration events.
/// Two approaches available:
/// - Option 1 (preferred, active): Explicit per-message-type Listen (matches Catalogs' explicit publish)
/// - Option 2 (commented): Direct Wolverine Kafka API wiring in ApplicationConfiguration
/// </summary>
public static class WolverineKafkaOrdersTopologyExtensions
{
    /// <summary>
    /// Configures Orders as consumer with snake_case naming.
    /// Consumes both <c>ProductCreatedV1</c> and <c>OrderSubmittedV1</c>
    /// via explicit per-message-type listeners.
    /// </summary>
    public static WolverineKafkaRegistrationBuilder ConfigureOrdersConsumeTopology(
        this WolverineKafkaRegistrationBuilder builder
    )
    {
        // ── Option 1 (preferred): Explicit per-message-type listener ──
        // Transparent, debuggable topology. Repeat per consumed type.
        // Topic auto-derived via snake_case convention; consumer group explicit.

        builder.UseSnakeCaseConventions();

        builder.Listen<MessageEnvelope<ProductCreatedV1>>(
            topicName: null, // auto-derived: product_created_v1
            MessagingConstants.OrdersProductsConsumerGroup
        );

        builder.Listen<MessageEnvelope<OrderSubmittedV1>>(
            topicName: null, // auto-derived: order_submitted_v1
            MessagingConstants.OrdersOrdersConsumerGroup
        );

        // ── Option 2: Direct Wolverine Kafka API ──
        // Wire listener directly via options.ListenToKafkaTopic() in
        // ApplicationConfiguration — no builder abstraction.
        //
        // options.ListenToKafkaTopic("product_created_v1")
        //     .UseDurableInbox()
        //     .ConfigureConsumer(config => config.GroupId = "orders-products")
        //     .DefaultIncomingMessage<MessageEnvelope<ProductCreatedV1>>();

        return builder;
    }
}
