using ECommerce.BuildingBlocks.TestHost;
using Tests.Shared.Fixtures;

namespace ECommerce.BuildingBlocks.RabbitMQ.IntegrationTests;

/// <summary>
/// Shared fixture for the RabbitMQ building-block integration tests.
/// Boots RabbitMQ, then starts the isolated <see cref="Program"/> test host
/// configured for RabbitMQ. No durable storage is used: the building-block
/// tests exercise publish/consume round-trips, so Wolverine runs with its
/// in-memory message store (no Postgres polling agents, no Respawn conflict).
/// The host wires the manual topology via
/// <c>AddWolverineRabbitMq(..., configure: ConfigureTestRabbitMqTopology)</c>.
/// </summary>
public sealed class RabbitMqBuildingBlocksSharedFixture()
    : SharedFixture<Program>(useRabbitMq: true)
{
    protected override void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary)
    {
        dictionary["WolverineBusOptions__TransportType"] = "rabbitmq";
        dictionary["WolverineBusOptions__AutoConfigMessagesTopology"] = "false";
        dictionary["WolverineBusOptions__UseEntityFrameworkCoreTransactions"] = "false";
        dictionary["WolverineBusOptions__UseDurableLocalQueues"] = "false";
        dictionary["ConnectionStrings__rabbitmq"] = RabbitMq!.ConnectionString;
    }

    protected override void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary)
    {
        dictionary["WolverineBusOptions:TransportType"] = "rabbitmq";
        dictionary["WolverineBusOptions:AutoConfigMessagesTopology"] = "false";
        dictionary["WolverineBusOptions:UseEntityFrameworkCoreTransactions"] = "false";
        dictionary["WolverineBusOptions:UseDurableLocalQueues"] = "false";
        dictionary["ConnectionStrings:rabbitmq"] = RabbitMq!.ConnectionString;
    }
}
