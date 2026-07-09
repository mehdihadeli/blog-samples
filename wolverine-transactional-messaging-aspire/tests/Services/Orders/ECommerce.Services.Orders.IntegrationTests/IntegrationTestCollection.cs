using Tests.Shared.Fixtures;
using Xunit;

namespace ECommerce.Services.Orders.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection
    : ICollectionFixture<PostgresContainerFixture>,
        ICollectionFixture<RabbitMqContainerFixture>,
        ICollectionFixture<KafkaContainerFixture>
{
    public const string Name = "orders-integration-tests";
}
