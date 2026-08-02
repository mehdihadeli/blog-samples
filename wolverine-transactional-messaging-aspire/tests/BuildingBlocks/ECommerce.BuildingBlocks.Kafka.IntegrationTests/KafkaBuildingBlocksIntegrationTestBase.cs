using ECommerce.BuildingBlocks.TestHost;
using Tests.Shared.TestBase;

namespace ECommerce.BuildingBlocks.Kafka.IntegrationTests;

[Collection(KafkaBuildingBlocksCollection.Name)]
public abstract class KafkaBuildingBlocksIntegrationTestBase(
    KafkaBuildingBlocksSharedFixture sharedFixture
) : BuildingBlocksIntegrationTestBase<Program, KafkaBuildingBlocksSharedFixture>(sharedFixture);
