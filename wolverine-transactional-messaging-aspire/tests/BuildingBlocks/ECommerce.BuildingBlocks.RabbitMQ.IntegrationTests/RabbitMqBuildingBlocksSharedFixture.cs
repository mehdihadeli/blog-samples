using ECommerce.BuildingBlocks.TestHost;
using Tests.Shared.Fixtures;

namespace ECommerce.BuildingBlocks.RabbitMQ.IntegrationTests;

/// <summary>
/// Shared fixture for the RabbitMQ building-block integration tests.
/// Boots Postgres (Wolverine durable storage) + RabbitMQ, then starts the
/// isolated <see cref="Program"/> test host configured for RabbitMQ.
/// The host wires the manual topology via
/// <c>AddWolverineRabbitMq(..., configure: ConfigureTestRabbitMqTopology)</c>.
/// </summary>
public sealed class RabbitMqBuildingBlocksSharedFixture()
    : SharedFixture<Program>(usePostgres: true, useRabbitMq: true)
{
    protected override void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary)
    {
        dictionary["WolverineBusOptions__TransportType"] = "rabbitmq";
        dictionary["WolverineBusOptions__AutoConfigMessagesTopology"] = "false";
        dictionary["WolverineBusOptions__UseEntityFrameworkCoreTransactions"] = "false";
        dictionary["WolverineBusOptions__UseDurableLocalQueues"] = "false";
        dictionary["ConnectionStrings__messaging-durable-storage"] =
            $"{Postgres!.ConnectionString};SSL Mode=Disable";
        dictionary["ConnectionStrings__rabbitmq"] = RabbitMq!.ConnectionString;
    }

    protected override void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary)
    {
        dictionary["WolverineBusOptions:TransportType"] = "rabbitmq";
        dictionary["WolverineBusOptions:AutoConfigMessagesTopology"] = "false";
        dictionary["WolverineBusOptions:UseEntityFrameworkCoreTransactions"] = "false";
        dictionary["WolverineBusOptions:UseDurableLocalQueues"] = "false";
        dictionary["ConnectionStrings:messaging-durable-storage"] =
            $"{Postgres!.ConnectionString};SSL Mode=Disable";
        dictionary["ConnectionStrings:rabbitmq"] = RabbitMq!.ConnectionString;
    }
}
