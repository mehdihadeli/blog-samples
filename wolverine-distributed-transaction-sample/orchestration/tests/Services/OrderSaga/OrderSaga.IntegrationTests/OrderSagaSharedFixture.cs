using Tests.Shared.Factory;
using Tests.Shared.Fixtures;

namespace OrderSaga.IntegrationTests;

public sealed class OrderSagaSharedFixture : SharedFixture<Program>
{
    public OrderSagaSharedFixture() { }

    protected override void ConfigureFactory(
        CustomWebApplicationFactory<Program> factory, string transport)
    {
        // No OrderSaga-specific overrides needed — base sets all standard connection strings.
    }
}
