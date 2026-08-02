using BuildingBlocks.Core.Extensions;
using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Kafka.Extensions;
using BuildingBlocks.Integration.Wolverine.RabbitMQ.Extensions;
using ECommerce.BuildingBlocks.TestHost.Messaging;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Transport is selected per test project via configuration overrides:
//   RabbitMQ tests → WolverineBusOptions:TransportType = rabbitmq
//   Kafka tests    → WolverineBusOptions:TransportType = kafka
var wolverineOptions = builder.Configuration.BindOptions<WolverineBusOptions>(
    nameof(WolverineBusOptions)
);

switch (wolverineOptions.TransportType)
{
    case MessagingTransportType.RabbitMq:
        builder.AddWolverineRabbitMq(
            wolverineBusOptions =>
            {
                wolverineBusOptions.ConnectionName = "rabbitmq";
                wolverineBusOptions.UseDurableLocalQueues = false;
                wolverineBusOptions.UseEntityFrameworkCoreTransactions = false;
                wolverineBusOptions.DurableStorageConnectionString =
                    GetDurableStorageConnectionString(builder) ?? string.Empty;
            },
            // Manual topology: exercises the RabbitMQ building-block builder API.
            configure: rabbitMq => rabbitMq.ConfigureTestRabbitMqTopology(),
            assemblies: [typeof(Program).Assembly]
        );
        break;

    case MessagingTransportType.Kafka:
        builder.AddWolverineKafka(
            wolverineBusOptions =>
            {
                wolverineBusOptions.ConnectionName = "kafka";
                wolverineBusOptions.UseDurableLocalQueues = false;
                wolverineBusOptions.UseEntityFrameworkCoreTransactions = false;
                wolverineBusOptions.DurableStorageConnectionString =
                    GetDurableStorageConnectionString(builder) ?? string.Empty;
            },
            // Manual topology: exercises the Kafka building-block builder API.
            configure: kafka => kafka.ConfigureTestKafkaTopology(),
            assemblies: [typeof(Program).Assembly]
        );
        break;

    default:
        throw new InvalidOperationException(
            $"Unsupported messaging transport type '{wolverineOptions.TransportType}'."
        );
}

var app = builder.Build();

app.Run();

// Wolverine's durable storage (wolverine schema) always lives in Postgres; no EF
// DbContext is registered in this host (UseEntityFrameworkCoreTransactions=false).
// The isolated building-block tests run WITHOUT durable storage, so a missing
// connection string is fine — Wolverine then uses its in-memory message store.
static string? GetDurableStorageConnectionString(WebApplicationBuilder builder)
{
    return builder.Configuration.GetConnectionString("messaging-durable-storage");
}

namespace ECommerce.BuildingBlocks.TestHost
{
    public partial class Program;
}
