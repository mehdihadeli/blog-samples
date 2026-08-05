using ECommerce.BuildingBlocks.TestHost;
using Tests.Shared.Fixtures;

namespace ECommerce.BuildingBlocks.Kafka.IntegrationTests;

/// <summary>
/// Shared fixture for the Kafka building-block integration tests.
/// Boots Kafka, then starts the isolated <see cref="Program"/> test host
/// configured for Kafka. No durable storage is used: the building-block tests
/// exercise publish/consume round-trips, so Wolverine runs with its in-memory
/// message store (no Postgres polling agents, no Respawn conflict).
/// The host wires the manual topology via
/// <c>AddWolverineKafka(..., configure: ConfigureTestKafkaTopology)</c>.
/// </summary>
public sealed class KafkaBuildingBlocksSharedFixture() : SharedFixture<Program>(useKafka: true)
{
    protected override void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary)
    {
        dictionary["WolverineBusOptions__TransportType"] = "kafka";
        dictionary["WolverineBusOptions__AutoConfigMessagesTopology"] = "false";
        dictionary["WolverineBusOptions__UseEntityFrameworkCoreTransactions"] = "false";
        dictionary["WolverineBusOptions__UseDurableLocalQueues"] = "false";
        dictionary["ConnectionStrings__kafka"] = Kafka!.BootstrapServers;
    }

    protected override void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary)
    {
        dictionary["WolverineBusOptions:TransportType"] = "kafka";
        dictionary["WolverineBusOptions:AutoConfigMessagesTopology"] = "false";
        dictionary["WolverineBusOptions:UseEntityFrameworkCoreTransactions"] = "false";
        dictionary["WolverineBusOptions:UseDurableLocalQueues"] = "false";
        dictionary["ConnectionStrings:kafka"] = Kafka!.BootstrapServers;
    }

    /// <summary>
    /// Keep the Kafka topics alive across the collection. The shared host starts once
    /// and Wolverine's AutoProvision only creates topics at startup, so deleting them
    /// between tests would leave listeners subscribed to removed partitions and break
    /// every subsequent round-trip.
    /// </summary>
    protected override bool ResetBrokerStateBetweenTests => false;
}
