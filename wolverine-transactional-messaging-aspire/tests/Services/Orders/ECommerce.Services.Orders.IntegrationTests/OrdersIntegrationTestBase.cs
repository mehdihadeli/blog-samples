using ECommerce.Services.Orders.Shared.Data;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;
using Tests.Shared.TestBase;

namespace ECommerce.Services.Orders.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public abstract class OrdersIntegrationTestBase : IntegrationTestBase<Program>
{
    protected OrdersIntegrationTestBase(
        PostgresContainerFixture postgres,
        RabbitMqContainerFixture rabbitMq,
        KafkaContainerFixture kafka
    )
        : base(postgres, rabbitMq, kafka) { }

    protected virtual string MessagingTransport => "rabbitmq";

    protected override bool UsesKafkaTransport =>
        string.Equals(MessagingTransport, "kafka", StringComparison.OrdinalIgnoreCase);

    protected override void ConfigureFactory(CustomWebApplicationFactory<Program> factory)
    {
        factory
            .WithSetting("Messaging:Transport", MessagingTransport)
            .WithSetting("ConnectionStrings:ordersdb", Postgres.ConnectionString);

        if (UsesKafkaTransport)
        {
            factory.WithSetting("ConnectionStrings:kafka", Kafka.BootstrapServers);
        }
        else
        {
            factory.WithSetting("ConnectionStrings:rabbitmq", RabbitMq.ConnectionString);
        }
    }

    protected Task ExecuteOrdersDbContextAsync(Func<OrdersDbContext, Task> action)
    {
        return ExecuteDbContextAsync(action);
    }

    protected Task<TResult> ExecuteOrdersDbContextAsync<TResult>(
        Func<OrdersDbContext, Task<TResult>> action
    )
    {
        return ExecuteDbContextAsync<OrdersDbContext, TResult>(action);
    }
}
