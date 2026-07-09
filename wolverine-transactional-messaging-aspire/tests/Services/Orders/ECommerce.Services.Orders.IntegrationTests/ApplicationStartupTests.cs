using ECommerce.Services.Orders;
using ECommerce.Services.Orders.Shared.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;
using Wolverine;

namespace ECommerce.Services.Orders.IntegrationTests;

public class ApplicationStartupTests(
    PostgresContainerFixture postgres,
    RabbitMqContainerFixture rabbitMq,
    KafkaContainerFixture kafka
) : OrdersIntegrationTestBase(postgres, rabbitMq, kafka)
{
    [Fact]
    public async Task AddApplicationServices_ShouldBuild_ForRabbitMq()
    {
        await RabbitMq.EnsureStartedAsync();

        using var appFactory = new CustomWebApplicationFactory<Program>()
            .WithSetting("Messaging:Transport", "rabbitmq")
            .WithSetting("ConnectionStrings:ordersdb", Postgres.ConnectionString)
            .WithSetting("ConnectionStrings:rabbitmq", RabbitMq.ConnectionString);

        await using var scope = appFactory.Services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OrdersDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMessageBus>());
        Assert.Equal(RabbitMq.ConnectionString, configuration.GetConnectionString("rabbitmq"));
    }

    [Fact]
    public async Task AddApplicationServices_ShouldBuild_ForKafka()
    {
        await Kafka.EnsureStartedAsync();

        using var appFactory = new CustomWebApplicationFactory<Program>()
            .WithSetting("Messaging:Transport", "kafka")
            .WithSetting("ConnectionStrings:ordersdb", Postgres.ConnectionString)
            .WithSetting("ConnectionStrings:kafka", Kafka.BootstrapServers);

        await using var scope = appFactory.Services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OrdersDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMessageBus>());
        Assert.Equal(Kafka.BootstrapServers, configuration.GetConnectionString("kafka"));
    }

    [Fact]
    public void AddApplicationServices_ShouldThrow_ForUnsupportedTransport()
    {
        using var appFactory = new CustomWebApplicationFactory<Program>()
            .WithSetting("Messaging:Transport", "invalid-broker")
            .WithSetting("ConnectionStrings:ordersdb", Postgres.ConnectionString);

        var exception = Record.Exception(() => _ = appFactory.Server);

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("Unsupported messaging transport", exception.Message);
    }
}
