using BuildingBlocks.Integration.Wolverine.RabbitMQ.Extensions;
using ECommerce.BuildingBlocks.RabbitMQ.IntegrationTests.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Tests.Shared.Fixtures;

namespace ECommerce.BuildingBlocks.RabbitMQ.IntegrationTests;

/// <summary>
/// Shared fixture for the RabbitMQ building-block integration tests.
/// Boots a real RabbitMQ container, then builds its OWN host with
/// <c>WebApplication.CreateBuilder</c> (no shared test-host project) and wires
/// the RabbitMQ building block with the manual topology via
/// <c>AddWolverineRabbitMq(..., configure: ConfigureTestRabbitMqTopology)</c>.
/// No durable storage is used: the building-block tests exercise
/// publish/consume round-trips, so Wolverine runs with its in-memory message
/// store (no Postgres polling agents, no Respawn conflict).
/// </summary>
public sealed class RabbitMqBuildingBlocksSharedFixture()
    : BuildingBlocksSharedFixture(useRabbitMq: true)
{
    protected override void ConfigureBuilder(WebApplicationBuilder builder)
    {
        // The building block reads the transport connection string from
        // configuration ("rabbitmq" → UseRabbitMqUsingNamedConnection).
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:rabbitmq"] = RabbitMq!.ConnectionString,
            }
        );

        builder.AddWolverineRabbitMq(
            wolverineBusOptions =>
            {
                wolverineBusOptions.ConnectionName = "rabbitmq";
                wolverineBusOptions.AutoConfigMessagesTopology = false;
                wolverineBusOptions.UseDurableLocalQueues = false;
                wolverineBusOptions.UseEntityFrameworkCoreTransactions = false;
            },
            // Manual topology: exercises the RabbitMQ building-block builder API.
            configure: rabbitMq => rabbitMq.ConfigureTestRabbitMqTopology(),
            // Handler + message discovery: the test project assembly (and with it
            // the topology above) is registered explicitly — Wolverine never finds
            // handlers in a test-runner assembly by itself.
            assemblies: [typeof(RabbitMqBuildingBlocksSharedFixture).Assembly]
        );
    }
}
