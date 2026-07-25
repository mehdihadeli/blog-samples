using Tests.Shared.TestBase;

namespace Payment.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public abstract class PaymentIntegrationTestBase
    : IntegrationTestBase<Program, PaymentSharedFixture>
{
    protected PaymentIntegrationTestBase(PaymentSharedFixture sharedFixture)
        : base(sharedFixture) { }
}
