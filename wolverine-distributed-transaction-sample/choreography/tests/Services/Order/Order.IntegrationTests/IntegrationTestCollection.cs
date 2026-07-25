using Tests.Shared.TestBase;

namespace Order.IntegrationTests;

[CollectionDefinition("integration-tests")]
public sealed class OrderIntegrationTestCollection : ICollectionFixture<OrderSharedFixture>;
