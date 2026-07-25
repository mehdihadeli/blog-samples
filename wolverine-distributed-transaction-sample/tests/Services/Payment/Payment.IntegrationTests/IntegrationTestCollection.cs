using Xunit;

namespace Payment.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<PaymentSharedFixture>
{
    public const string Name = "payment-integration-tests";
}
