using BuildingBlocks.Core.Messages;
using BuildingBlocks.Integration.Wolverine.Kafka;
using Confluent.Kafka.Admin;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using Humanizer;

namespace ECommerce.Services.Catalogs;

/// <summary>
/// Catalogs' Kafka manual publish topology (OPTION 1).
/// Used when <c>AutoConfigMessagesTopology = false</c> — passed as
/// the <c>configure</c> callback to <c>AddWolverineKafka</c>.
///
/// OPTION 2 (auto-scan) is handled by
/// <c>ApplyMessagesPublishTopology</c> in the building blocks layer —
/// scans assemblies for <c>IIntegrationEvent</c> types automatically.
/// </summary>
public static class WolverineKafkaCatalogsTopologyExtensions
{
    /// <summary>
    /// Explicit per-message-type publish topology with snake_case naming.
    /// Publishes <c>ProductCreatedV1</c> and <c>OrderSubmittedV1</c>.
    /// </summary>
    public static WolverineKafkaRegistrationBuilder ConfigureCatalogsPublishTopology(
        this WolverineKafkaRegistrationBuilder builder
    )
    {
        // OPTION 1 (manual): Explicit per-message-type topology.
        // Transparent, debuggable — repeat per message type.
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

        return builder;
    }
}
