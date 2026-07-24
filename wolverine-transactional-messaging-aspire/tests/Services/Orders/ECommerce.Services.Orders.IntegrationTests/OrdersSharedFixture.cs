using Tests.Shared.Fixtures;

namespace ECommerce.Services.Orders.IntegrationTests;

public class OrdersSharedFixture()
    : SharedFixture<Program>(usePostgres: true, useRabbitMq: true, useKafka: true)
{
    protected override void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary)
    {
        dictionary["ConnectionStrings__ordersdb"] = Postgres!.ConnectionString;
        if (RabbitMq is not null)
            dictionary["ConnectionStrings__rabbitmq"] = RabbitMq.ConnectionString;
        if (Kafka is not null)
            dictionary["ConnectionStrings__kafka"] = Kafka!.BootstrapServers;
    }

    protected override void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary)
    {
        dictionary["ConnectionStrings:ordersdb"] = Postgres!.ConnectionString;
        if (RabbitMq is not null)
            dictionary["ConnectionStrings:rabbitmq"] = RabbitMq.ConnectionString;
        if (Kafka is not null)
            dictionary["ConnectionStrings:kafka"] = Kafka!.BootstrapServers;
    }
}
