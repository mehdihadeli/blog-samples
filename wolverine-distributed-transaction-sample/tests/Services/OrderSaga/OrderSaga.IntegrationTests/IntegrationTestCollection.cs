using Xunit;

namespace OrderSaga.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<OrderSagaSharedFixture>
{
    public const string Name = "order-saga-integration-tests";
}
