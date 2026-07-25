using Tests.Shared.TestBase;

namespace OrderSaga.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public abstract class OrderSagaIntegrationTestBase
    : IntegrationTestBase<Program, OrderSagaSharedFixture>
{
    protected OrderSagaIntegrationTestBase(OrderSagaSharedFixture sharedFixture)
        : base(sharedFixture) { }
}
