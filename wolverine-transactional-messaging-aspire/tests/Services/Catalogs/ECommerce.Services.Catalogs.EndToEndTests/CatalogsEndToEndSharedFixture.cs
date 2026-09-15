using Tests.Shared.Fixtures;

namespace ECommerce.Services.Catalogs.EndToEndTests;

public sealed class CatalogsEndToEndSharedFixture()
    : SharedFixture<Program>(usePostgres: true, useRabbitMq: true, useKafka: true, useMongo: true)
{
    public string MongoConnectionString =>
        Mongo?.ConnectionString
        ?? throw new InvalidOperationException("MongoDB fixture is not configured.");

    protected override void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary)
    {
        dictionary["ConnectionStrings__catalogsdb"] = Postgres!.ConnectionString;
        dictionary["ConnectionStrings__catalogs-mongo"] = MongoConnectionString;
        dictionary["ConnectionStrings__rabbitmq"] = RabbitMq!.ConnectionString;
        dictionary["ConnectionStrings__kafka"] = Kafka!.BootstrapServers;
    }

    protected override void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary)
    {
        dictionary["ConnectionStrings:catalogsdb"] = Postgres!.ConnectionString;
        dictionary["ConnectionStrings:catalogs-mongo"] = MongoConnectionString;
        dictionary["ConnectionStrings:rabbitmq"] = RabbitMq!.ConnectionString;
        dictionary["ConnectionStrings:kafka"] = Kafka!.BootstrapServers;
    }
}
