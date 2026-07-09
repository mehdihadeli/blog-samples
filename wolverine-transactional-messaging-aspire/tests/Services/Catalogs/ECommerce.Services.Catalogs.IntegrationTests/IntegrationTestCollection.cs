using Tests.Shared.Fixtures;
using Xunit;

namespace ECommerce.Services.Catalogs.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection
    : ICollectionFixture<PostgresContainerFixture>,
        ICollectionFixture<RabbitMqContainerFixture>,
        ICollectionFixture<KafkaContainerFixture>,
        ICollectionFixture<MongoContainerFixture>
{
    public const string Name = "catalogs-integration-tests";
}
