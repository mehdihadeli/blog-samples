using Tests.Shared.Factory;
using Tests.Shared.Fixtures;

namespace Payment.IntegrationTests;

public sealed class PaymentSharedFixture : SharedFixture<Program>
{
    public PaymentSharedFixture() { }

    protected override void ConfigureFactory(
        CustomWebApplicationFactory<Program> factory, string transport)
    {
        // No Payment-specific overrides needed.
    }
}
