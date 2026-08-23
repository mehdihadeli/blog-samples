using BuildingBlocks.Integration.Wolverine.Kafka.Extensions;
using ECommerce.BuildingBlocks.Kafka.IntegrationTests.Messaging;
using Microsoft.AspNetCore.Builder;
using Tests.Shared.Fixtures;

namespace ECommerce.BuildingBlocks.Kafka.IntegrationTests;

/// <summary>
/// Shared fixture for the Kafka building-block integration tests.
/// Boots a real Kafka container, then builds its OWN host with
/// <c>WebApplication.CreateBuilder</c> (no shared test-host project) and wires
/// the Kafka building block with the manual topology via
/// <c>AddWolverineKafka(..., configure: ConfigureTestKafkaTopology)</c>.
/// No durable storage is used: the building-block tests exercise
/// publish/consume round-trips, so Wolverine runs with its in-memory
/// message store (no Postgres polling agents, no Respawn conflict).
/// </summary>
public sealed class KafkaBuildingBlocksSharedFixture() : BuildingBlocksSharedFixture(useKafka: true)
{
    protected override void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.AddWolverineKafka(
            wolverineBusOptions =>
            {
                // Use the container's mapped address directly. Named Wolverine
                // connections are resolved by the application's connection
                // conventions and can leave this standalone test host waiting
                // forever for a broker that is actually ready.
                wolverineBusOptions.ConnectionString = Kafka!.BootstrapServers;
                wolverineBusOptions.AutoConfigMessagesTopology = false;
                wolverineBusOptions.UseDurableLocalQueues = false;
                wolverineBusOptions.UseEntityFrameworkCoreTransactions = false;
            },
            // Manual topology: exercises the Kafka building-block builder API.
            configure: kafka => kafka.ConfigureTestKafkaTopology(),
            // Handler + message discovery: the test project assembly (and with it
            // the topology above) is registered explicitly — Wolverine never finds
            // handlers in a test-runner assembly by itself.
            assemblies: [typeof(KafkaBuildingBlocksSharedFixture).Assembly]
        );
    }

    /// <summary>
    /// Keep the Kafka topics alive across the collection. The shared host starts once
    /// and Wolverine's AutoProvision only creates topics at startup, so deleting them
    /// between tests would leave listeners subscribed to removed partitions and break
    /// every subsequent round-trip.
    /// </summary>
    protected override bool ResetBrokerStateBetweenTests => false;
}
