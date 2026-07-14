using ECommerce.Services.Orders.Shared.Data;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;

namespace ECommerce.Services.Orders.IntegrationTests;

public sealed class OrdersSharedFixture : SharedFixture<Program>
{
    protected override string DefaultTransport => "kafka";

    public Task ExecuteOrdersDbContextAsync(Func<OrdersDbContext, Task> action) =>
        ExecuteOrdersDbContextInternalAsync(action);

    public Task<TResult> ExecuteOrdersDbContextAsync<TResult>(
        Func<OrdersDbContext, Task<TResult>> action
    ) => ExecuteOrdersDbContextInternalAsync(action);

    private async Task ExecuteOrdersDbContextInternalAsync(Func<OrdersDbContext, Task> action)
    {
        using var factory = CreateFactory(DefaultTransport);
        await ExecuteDbContextAsync(factory, action);
    }

    private async Task<TResult> ExecuteOrdersDbContextInternalAsync<TResult>(
        Func<OrdersDbContext, Task<TResult>> action
    )
    {
        using var factory = CreateFactory(DefaultTransport);
        return await ExecuteDbContextAsync(factory, action);
    }

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
