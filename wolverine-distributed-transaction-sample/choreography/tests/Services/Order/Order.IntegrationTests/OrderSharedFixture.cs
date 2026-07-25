using Tests.Shared;
using Tests.Shared.Factory;

namespace Order.IntegrationTests;

public sealed class OrderSharedFixture : SharedFixture
{
    public CustomWebApplicationFactory<Program> CreateFactory(string connectionString)
    {
        return new CustomWebApplicationFactory<Program>()
            .WithSetting("ConnectionStrings:ordersdb", connectionString)
            .WithSetting("Messaging:Transport", "rabbitmq");
    }
}
