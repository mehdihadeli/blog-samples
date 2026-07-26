using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.Wolverine.Kafka;
using Confluent.Kafka.Admin;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using Humanizer;

namespace ECommerce.Services.Catalogs;

/// <summary>
/// Catalogs' Kafka topology — publishes integration events.
/// Two approaches available:
/// - Option 1 (preferred, active): Explicit per-message-type topology (mirrors RabbitMQ's
///   <c>PublishToExchange + DeclareExchange</c> pattern)
/// - Option 2 (commented): Wolverine bulk auto-routing — routes ALL messages to
///   Kafka topics by type name automatically (no per-type config)
/// </summary>
public static class WolverineKafkaCatalogsTopologyExtensions
{
    /// <summary>
    /// Configures Catalogs as publisher with snake_case naming.
    /// Publishes both <c>ProductCreatedV1</c> and <c>OrderSubmittedV1</c>
    /// via explicit per-message-type topology.
    /// </summary>
    public static WolverineKafkaRegistrationBuilder ConfigureCatalogsPublishTopology(
        this WolverineKafkaRegistrationBuilder builder
    )
    {
        // ── Option 1 (preferred): Explicit per-message-type topology ──
        // Transparent, debuggable topology. Repeat per message type.
        // Mirrors RabbitMQ's PublishToExchange + DeclareExchange pattern.
        // Names derived via Humanizer.Underscore().

        builder.UseSnakeCaseConventions();

        builder.PublishToTopic<MessageEnvelope<ProductCreatedV1>>(
            nameof(ProductCreatedV1).Underscore(),
            spec =>
            {
                spec.NumPartitions = 3;
                spec.ReplicationFactor = 1;
            }
        );

        builder.PublishToTopic<MessageEnvelope<OrderSubmittedV1>>(
            nameof(OrderSubmittedV1).Underscore(),
            spec =>
            {
                spec.NumPartitions = 3;
                spec.ReplicationFactor = 1;
            }
        );

        // ── Option 2: Wolverine bulk auto-routing ──
        // Routes ALL published messages to Kafka topics by type name
        // automatically — no per-message-type config needed.
        //
        // builder.PublishAllMessages();

        return builder;
    }
}
