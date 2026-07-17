using Tests.Shared.Factory;
using Tests.Shared.Fixtures;

namespace ECommerce.Services.Orders.IntegrationTests;

/// <summary>
/// Collection-scoped shared fixture for the Orders integration test suite.
/// Manages Postgres, RabbitMQ and Kafka container lifecycles and creates
/// WebApplicationFactory instances pre-configured with the correct
/// connection strings for each transport.
/// </summary>
public sealed class OrdersSharedFixture : SharedFixture<Program>
{
    protected override void ConfigureFactory(
        CustomWebApplicationFactory<Program> factory,
        string transport
    )
    {
        factory
            .WithSetting("Messaging:Transport", transport)
            .WithSetting("ConnectionStrings:ordersdb", Postgres.ConnectionString);

        if (string.Equals(transport, "kafka", StringComparison.OrdinalIgnoreCase))
        {
            factory.WithSetting("ConnectionStrings:kafka", Kafka.BootstrapServers);
            return;
        }

        factory.WithSetting("ConnectionStrings:rabbitmq", RabbitMq.ConnectionString);
    }
}
