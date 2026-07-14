using ECommerce.Services.Orders;
using ECommerce.Services.Orders.Shared.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace ECommerce.Services.Orders.IntegrationTests;

public class ApplicationStartupTests(OrdersSharedFixture sharedFixture)
    : OrdersIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task AddApplicationServices_ShouldBuild_ForRabbitMq()
    {
        using var appFactory = SharedFixture.CreateFactory("rabbitmq");

        await using var scope = appFactory.Services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OrdersDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMessageBus>());
        Assert.Equal(RabbitMq.ConnectionString, configuration.GetConnectionString("rabbitmq"));
    }

    [Fact]
    public async Task AddApplicationServices_ShouldBuild_ForKafka()
    {
        using var appFactory = SharedFixture.CreateFactory("kafka");

        await using var scope = appFactory.Services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OrdersDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMessageBus>());
        Assert.Equal(Kafka.BootstrapServers, configuration.GetConnectionString("kafka"));
    }

    [Fact]
    public void AddApplicationServices_ShouldThrow_ForUnsupportedTransport()
    {
        using var appFactory = SharedFixture.CreateFactory("invalid-broker");

        var exception = Record.Exception(() => _ = appFactory.Server);

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("Unsupported messaging transport", exception.Message);
    }
}
