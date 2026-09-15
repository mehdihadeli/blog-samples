using Tests.Shared.Fixtures;

namespace ECommerce.Services.Catalogs.IntegrationTests;

public sealed class CatalogsSharedFixture()
    : SharedFixture<Program>(usePostgres: true, useRabbitMq: true, useKafka: true, useMongo: true)
{
    public string MongoConnectionString =>
        Mongo?.ConnectionString
        ?? throw new InvalidOperationException("MongoDB fixture is not configured.");

    protected override void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary)
    {
        dictionary[IntegrationTestConfigurations.CatalogsDatabase] = Postgres!.ConnectionString;
        dictionary[IntegrationTestConfigurations.CatalogsMongoDatabase] = MongoConnectionString;
        if (RabbitMq is not null)
            dictionary[IntegrationTestConfigurations.RabbitMq] = RabbitMq.ConnectionString;
        if (Kafka is not null)
            dictionary[IntegrationTestConfigurations.Kafka] = Kafka!.BootstrapServers;
    }

    protected override void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary)
    {
        dictionary[IntegrationTestConfigurations.CatalogsDatabase.Replace("__", ":")] =
            Postgres!.ConnectionString;
        dictionary[IntegrationTestConfigurations.CatalogsMongoDatabase.Replace("__", ":")] =
            MongoConnectionString;
        if (RabbitMq is not null)
            dictionary[IntegrationTestConfigurations.RabbitMq.Replace("__", ":")] =
                RabbitMq.ConnectionString;
        if (Kafka is not null)
            dictionary[IntegrationTestConfigurations.Kafka.Replace("__", ":")] =
                Kafka!.BootstrapServers;
    }
}
