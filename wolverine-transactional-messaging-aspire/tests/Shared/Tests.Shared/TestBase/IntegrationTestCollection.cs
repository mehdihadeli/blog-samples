using Tests.Shared.Fixtures;
using Xunit;

namespace Tests.Shared.TestBase;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection
    : ICollectionFixture<PostgresContainerFixture>,
        ICollectionFixture<RabbitMqContainerFixture>,
        ICollectionFixture<KafkaContainerFixture>
{
    public const string Name = "integration-tests";
}
