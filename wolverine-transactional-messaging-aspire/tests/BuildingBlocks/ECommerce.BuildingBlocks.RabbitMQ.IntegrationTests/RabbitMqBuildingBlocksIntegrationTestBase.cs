using ECommerce.BuildingBlocks.TestHost;
using Tests.Shared.TestBase;

namespace ECommerce.BuildingBlocks.RabbitMQ.IntegrationTests;

[Collection(RabbitMqBuildingBlocksCollection.Name)]
public abstract class RabbitMqBuildingBlocksIntegrationTestBase(
    RabbitMqBuildingBlocksSharedFixture sharedFixture
) : IntegrationTestBase<Program, RabbitMqBuildingBlocksSharedFixture>(sharedFixture);
