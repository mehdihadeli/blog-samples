using ECommerce.Services.Catalogs;
using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace ECommerce.Services.Catalogs.IntegrationTests;

public class ApplicationStartupTests(CatalogsSharedFixture sharedFixture)
    : CatalogsIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task AddApplicationServices_ShouldBuild_ForRabbitMq()
    {
        using var appFactory = SharedFixture.CreateFactory("rabbitmq");

        await using var scope = appFactory.Services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CatalogsDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductReadRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMessageBus>());
        Assert.Equal(RabbitMq.ConnectionString, configuration.GetConnectionString("rabbitmq"));
    }

    [Fact]
    public async Task AddApplicationServices_ShouldBuild_ForKafka()
    {
        using var appFactory = SharedFixture.CreateFactory("kafka");

        await using var scope = appFactory.Services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CatalogsDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductReadRepository>());
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
