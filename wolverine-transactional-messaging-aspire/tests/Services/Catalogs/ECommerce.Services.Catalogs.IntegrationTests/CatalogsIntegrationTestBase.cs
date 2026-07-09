using ECommerce.Services.Catalogs.Shared.Data;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;
using Tests.Shared.TestBase;

namespace ECommerce.Services.Catalogs.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public abstract class CatalogsIntegrationTestBase : IntegrationTestBase<Program>
{
    protected CatalogsIntegrationTestBase(
        PostgresContainerFixture postgres,
        RabbitMqContainerFixture rabbitMq,
        KafkaContainerFixture kafka,
        MongoContainerFixture mongo
    )
        : base(postgres, rabbitMq, kafka)
    {
        Mongo = mongo;
    }

    protected MongoContainerFixture Mongo { get; }

    protected virtual string MessagingTransport => "rabbitmq";

    protected override bool UsesKafkaTransport =>
        string.Equals(MessagingTransport, "kafka", StringComparison.OrdinalIgnoreCase);

    protected override void ConfigureFactory(CustomWebApplicationFactory<Program> factory)
    {
        factory
            .WithSetting("Messaging:Transport", MessagingTransport)
            .WithSetting("ConnectionStrings:catalogsdb", Postgres.ConnectionString)
            .WithSetting("ConnectionStrings:catalogs-mongo", Mongo.ConnectionString);

        if (UsesKafkaTransport)
        {
            factory.WithSetting("ConnectionStrings:kafka", Kafka.BootstrapServers);
        }
        else
        {
            factory.WithSetting("ConnectionStrings:rabbitmq", RabbitMq.ConnectionString);
        }
    }

    protected override Task ResetStateAsync()
    {
        return Mongo.ResetAsync();
    }

    protected Task ExecuteCatalogsDbContextAsync(Func<CatalogsDbContext, Task> action)
    {
        return ExecuteDbContextAsync(action);
    }

    protected Task<TResult> ExecuteCatalogsDbContextAsync<TResult>(
        Func<CatalogsDbContext, Task<TResult>> action
    )
    {
        return ExecuteDbContextAsync<CatalogsDbContext, TResult>(action);
    }
}
