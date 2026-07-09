using ECommerce.Services.Catalogs;
using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;
using Wolverine;

namespace ECommerce.Services.Catalogs.IntegrationTests;

public class ApplicationStartupTests(
    PostgresContainerFixture postgres,
    RabbitMqContainerFixture rabbitMq,
    KafkaContainerFixture kafka,
    MongoContainerFixture mongo
) : CatalogsIntegrationTestBase(postgres, rabbitMq, kafka, mongo)
{
    [Fact]
    public async Task AddApplicationServices_ShouldBuild_ForRabbitMq()
    {
        await RabbitMq.EnsureStartedAsync();

        using var appFactory = new CustomWebApplicationFactory<Program>()
            .WithSetting("Messaging:Transport", "rabbitmq")
            .WithSetting("ConnectionStrings:catalogsdb", Postgres.ConnectionString)
            .WithSetting("ConnectionStrings:catalogs-mongo", Mongo.ConnectionString)
            .WithSetting("ConnectionStrings:rabbitmq", RabbitMq.ConnectionString);

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
        await Kafka.EnsureStartedAsync();

        using var appFactory = new CustomWebApplicationFactory<Program>()
            .WithSetting("Messaging:Transport", "kafka")
            .WithSetting("ConnectionStrings:catalogsdb", Postgres.ConnectionString)
            .WithSetting("ConnectionStrings:catalogs-mongo", Mongo.ConnectionString)
            .WithSetting("ConnectionStrings:kafka", Kafka.BootstrapServers);

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
        using var appFactory = new CustomWebApplicationFactory<Program>()
            .WithSetting("Messaging:Transport", "invalid-broker")
            .WithSetting("ConnectionStrings:catalogsdb", Postgres.ConnectionString)
            .WithSetting("ConnectionStrings:catalogs-mongo", Mongo.ConnectionString);

        var exception = Record.Exception(() => _ = appFactory.Server);

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("Unsupported messaging transport", exception.Message);
    }
}
