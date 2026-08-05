using ECommerce.BuildingBlocks.TestHost;
using Tests.Shared.TestBase;

namespace ECommerce.BuildingBlocks.Kafka.IntegrationTests;

[Collection(KafkaBuildingBlocksCollection.Name)]
public abstract class KafkaBuildingBlocksIntegrationTestBase(
    KafkaBuildingBlocksSharedFixture sharedFixture
) : IntegrationTestBase<Program, KafkaBuildingBlocksSharedFixture>(sharedFixture);
