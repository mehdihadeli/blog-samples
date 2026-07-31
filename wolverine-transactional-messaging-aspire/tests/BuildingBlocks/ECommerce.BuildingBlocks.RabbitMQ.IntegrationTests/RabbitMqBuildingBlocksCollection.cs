using Xunit;

namespace ECommerce.BuildingBlocks.RabbitMQ.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class RabbitMqBuildingBlocksCollection
    : ICollectionFixture<RabbitMqBuildingBlocksSharedFixture>
{
    public const string Name = "building-blocks-rabbitmq-integration-tests";
}
