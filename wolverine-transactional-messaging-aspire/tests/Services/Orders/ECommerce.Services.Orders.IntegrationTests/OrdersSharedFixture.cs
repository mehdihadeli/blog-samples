using ECommerce.Services.Orders.Api;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared.Fixtures;

namespace ECommerce.Services.Orders.IntegrationTests;

public class OrdersSharedFixture()
    : SharedFixture<Program>(usePostgres: true, useRabbitMq: true, useKafka: true)
{
    protected override void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary)
    {
        dictionary[IntegrationTestConfigurations.OrdersDatabase] =
            $"{Postgres!.ConnectionString};SSL Mode=Disable";
        if (RabbitMq is not null)
            dictionary[IntegrationTestConfigurations.RabbitMq] = RabbitMq.ConnectionString;
        if (Kafka is not null)
            dictionary[IntegrationTestConfigurations.Kafka] = Kafka!.BootstrapServers;
    }

    protected override void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary)
    {
        dictionary[IntegrationTestConfigurations.OrdersDatabase.Replace("__", ":")] =
            $"{Postgres!.ConnectionString};SSL Mode=Disable";
        if (RabbitMq is not null)
            dictionary[IntegrationTestConfigurations.RabbitMq.Replace("__", ":")] =
                RabbitMq.ConnectionString;
        if (Kafka is not null)
            dictionary[IntegrationTestConfigurations.Kafka.Replace("__", ":")] =
                Kafka!.BootstrapServers;
    }
}
