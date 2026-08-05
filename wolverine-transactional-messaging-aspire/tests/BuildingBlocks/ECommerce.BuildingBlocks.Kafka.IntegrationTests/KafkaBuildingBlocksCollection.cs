using Xunit;

namespace ECommerce.BuildingBlocks.Kafka.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class KafkaBuildingBlocksCollection
    : ICollectionFixture<KafkaBuildingBlocksSharedFixture>
{
    public const string Name = "building-blocks-kafka-integration-tests";
}
