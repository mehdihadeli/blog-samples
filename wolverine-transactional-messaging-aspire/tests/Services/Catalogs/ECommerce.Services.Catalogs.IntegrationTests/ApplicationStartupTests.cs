using BuildingBlocks.Core.Messages;
using ECommerce.Services.Catalogs;
using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared.Factory;
using Wolverine;

namespace ECommerce.Services.Catalogs.IntegrationTests;

public class ApplicationStartupTests(CatalogsSharedFixture sharedFixture)
    : CatalogsIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task AddApplicationServices_ShouldBuild_ForRabbitMq()
    {
        using var appFactory = new CustomWebApplicationFactory<Program>().AddOverrideEnvKeyValues(
            dict =>
            {
                dict["ConnectionStrings__catalogsdb"] = SharedFixture.Postgres!.ConnectionString;
                dict["ConnectionStrings__catalogs-mongo"] = SharedFixture.MongoConnectionString;
                dict["ConnectionStrings__rabbitmq"] = SharedFixture.RabbitMq!.ConnectionString;
            }
        );

        await using var scope = appFactory.Services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CatalogsDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductReadRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMessageBus>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IExternalEventBus>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IBusDirectPublisher>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMessagePersistenceService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>());
        Assert.Equal(
            SharedFixture.RabbitMq!.ConnectionString,
            configuration.GetConnectionString("rabbitmq")
        );
    }

    [Fact]
    public async Task AddApplicationServices_ShouldBuild_ForKafka()
    {
        using var appFactory = new CustomWebApplicationFactory<Program>().AddOverrideEnvKeyValues(
            dict =>
            {
                dict["ConnectionStrings__catalogsdb"] = SharedFixture.Postgres!.ConnectionString;
                dict["ConnectionStrings__catalogs-mongo"] = SharedFixture.MongoConnectionString;
                dict["ConnectionStrings__kafka"] = SharedFixture.Kafka!.BootstrapServers;
                dict["WolverineBusOptions__TransportType"] = "kafka";
            }
        );

        await using var scope = appFactory.Services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CatalogsDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductReadRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMessageBus>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IExternalEventBus>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IBusDirectPublisher>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMessagePersistenceService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>());
        Assert.Equal(
            SharedFixture.Kafka!.BootstrapServers,
            configuration.GetConnectionString("kafka")
        );
    }

    [Fact]
    public async Task AddApplicationServices_ShouldThrow_ForUnsupportedTransport()
    {
        using var appFactory = new CustomWebApplicationFactory<Program>().AddOverrideEnvKeyValues(
            dict =>
            {
                dict["ConnectionStrings__catalogsdb"] = SharedFixture.Postgres!.ConnectionString;
                dict["ConnectionStrings__catalogs-mongo"] = SharedFixture.MongoConnectionString;
                dict["WolverineBusOptions__TransportType"] = "invalid-broker";
            }
        );

        var exception = await Record.ExceptionAsync(() => Task.Run(() => _ = appFactory.Server));

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }
}
