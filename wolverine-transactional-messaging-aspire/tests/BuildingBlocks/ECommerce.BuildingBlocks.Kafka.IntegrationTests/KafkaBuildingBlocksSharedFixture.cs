using ECommerce.BuildingBlocks.TestHost;
using Tests.Shared.Fixtures;

namespace ECommerce.BuildingBlocks.Kafka.IntegrationTests;

/// <summary>
/// Shared fixture for the Kafka building-block integration tests.
/// Boots Postgres (Wolverine durable storage) + Kafka, then starts the
/// isolated <see cref="Program"/> test host configured for Kafka.
/// The host wires the manual topology via
/// <c>AddWolverineKafka(..., configure: ConfigureTestKafkaTopology)</c>.
/// </summary>
public sealed class KafkaBuildingBlocksSharedFixture()
    : SharedFixture<Program>(usePostgres: true, useKafka: true)
{
    protected override void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary)
    {
        dictionary["WolverineBusOptions__TransportType"] = "kafka";
        dictionary["WolverineBusOptions__AutoConfigMessagesTopology"] = "false";
        dictionary["WolverineBusOptions__UseEntityFrameworkCoreTransactions"] = "false";
        dictionary["WolverineBusOptions__UseDurableLocalQueues"] = "false";
        dictionary["ConnectionStrings__messaging-durable-storage"] =
            $"{Postgres!.ConnectionString};SSL Mode=Disable";
        dictionary["ConnectionStrings__kafka"] = Kafka!.BootstrapServers;
    }

    protected override void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary)
    {
        dictionary["WolverineBusOptions:TransportType"] = "kafka";
        dictionary["WolverineBusOptions:AutoConfigMessagesTopology"] = "false";
        dictionary["WolverineBusOptions:UseEntityFrameworkCoreTransactions"] = "false";
        dictionary["WolverineBusOptions:UseDurableLocalQueues"] = "false";
        dictionary["ConnectionStrings:messaging-durable-storage"] =
            $"{Postgres!.ConnectionString};SSL Mode=Disable";
        dictionary["ConnectionStrings:kafka"] = Kafka!.BootstrapServers;
    }
}
