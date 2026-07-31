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
        dictionary["ConnectionStrings__catalogsdb"] = Postgres!.ConnectionString;
        dictionary["ConnectionStrings__catalogs-mongo"] = MongoConnectionString;
        if (RabbitMq is not null)
            dictionary["ConnectionStrings__rabbitmq"] = RabbitMq.ConnectionString;
        if (Kafka is not null)
            dictionary["ConnectionStrings__kafka"] = Kafka!.BootstrapServers;
    }

    protected override void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary)
    {
        dictionary["ConnectionStrings:catalogsdb"] = Postgres!.ConnectionString;
        dictionary["ConnectionStrings:catalogs-mongo"] = MongoConnectionString;
        if (RabbitMq is not null)
            dictionary["ConnectionStrings:rabbitmq"] = RabbitMq.ConnectionString;
        if (Kafka is not null)
            dictionary["ConnectionStrings:kafka"] = Kafka!.BootstrapServers;
    }
}
