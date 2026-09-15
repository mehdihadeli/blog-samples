using ECommerce.Services.Orders.Api;
using Tests.Shared.Fixtures;

namespace ECommerce.Services.Orders.EndToEndTests;

public sealed class OrdersEndToEndSharedFixture()
    : SharedFixture<Program>(usePostgres: true, useRabbitMq: true, useKafka: true)
{
    protected override void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary)
    {
        dictionary["ConnectionStrings__ordersdb"] = Postgres!.ConnectionString;
        dictionary["ConnectionStrings__rabbitmq"] = RabbitMq!.ConnectionString;
        dictionary["ConnectionStrings__kafka"] = Kafka!.BootstrapServers;
    }

    protected override void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary)
    {
        dictionary["ConnectionStrings:ordersdb"] = Postgres!.ConnectionString;
        dictionary["ConnectionStrings:rabbitmq"] = RabbitMq!.ConnectionString;
        dictionary["ConnectionStrings:kafka"] = Kafka!.BootstrapServers;
    }
}
