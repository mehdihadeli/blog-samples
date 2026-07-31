using BuildingBlocks.Core.Messages;
using BuildingBlocks.Integration.Wolverine.Kafka;
using Confluent.Kafka;
using ECommerce.BuildingBlocks.TestHost.Messages;
using Humanizer;

namespace ECommerce.BuildingBlocks.TestHost.Messaging;

/// <summary>
/// Manual Kafka topology that exercises the building block's builder API:
/// <c>PublishToTopic</c> (with topic specification), <c>Listen</c> (explicit
/// topic + consumer group), <c>UseSnakeCaseConventions</c> (auto-named
/// topic/group) and <c>WithNamingConvention</c> (custom topic names).
/// Wired in via <c>AddWolverineKafka(..., configure: ...)</c> when
/// <c>AutoConfigMessagesTopology = false</c>.
/// </summary>
public static class WolverineKafkaTestTopology
{
    public const string ProductCreatedTopic = "catalogs-products-created";
    public const string OrdersProductsConsumerGroup = "orders-products";

    public const string CustomTopicPrefix = "custom";

    public static WolverineKafkaRegistrationBuilder ConfigureTestKafkaTopology(
        this WolverineKafkaRegistrationBuilder builder
    )
    {
        // ── 1. Explicit topic + consumer group round-trip ────────────
        // PublishToTopic (with NumPartitions/ReplicationFactor spec) +
        // Listen(topicName, consumerGroupId). Earliest offset makes the
        // consumer pick up messages even if the topic was re-created after
        // a cleanup (auto.create.topics.enable on the broker).
        builder.PublishToTopic<MessageEnvelope<ProductCreatedV1>>(
            ProductCreatedTopic,
            specification =>
            {
                specification.NumPartitions = 1;
                specification.ReplicationFactor = 1;
            }
        );
        builder.Listen<MessageEnvelope<ProductCreatedV1>>(
            ProductCreatedTopic,
            OrdersProductsConsumerGroup,
            listener =>
                listener.ConfigureConsumer(config =>
                    config.AutoOffsetReset = AutoOffsetReset.Earliest
                )
        );

        // ── 2. Auto-naming (snake_case) round-trip ───────────────────
        // Publish<T>() + Listen<T>() → topic order_created_v1.
        builder.UseSnakeCaseConventions();
        builder.Publish<MessageEnvelope<OrderCreatedV1>>();
        builder.Listen<MessageEnvelope<OrderCreatedV1>>(listener =>
            listener.ConfigureConsumer(config => config.AutoOffsetReset = AutoOffsetReset.Earliest)
        );

        // ── 3. Custom naming convention round-trip ───────────────────
        // WithNamingConvention → topic custom-inventory_adjusted_v1.
        builder.WithNamingConvention(type => $"{CustomTopicPrefix}-{type.Name.Underscore()}");
        builder.Publish<MessageEnvelope<InventoryAdjustedV1>>();
        builder.Listen<MessageEnvelope<InventoryAdjustedV1>>(listener =>
            listener.ConfigureConsumer(config => config.AutoOffsetReset = AutoOffsetReset.Earliest)
        );

        return builder;
    }
}
