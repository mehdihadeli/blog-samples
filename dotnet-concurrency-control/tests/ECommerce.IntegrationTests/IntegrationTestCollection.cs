using Xunit;

namespace ECommerce.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<ECommerceSharedFixture>
{
    public const string Name = "ecommerce-integration-tests";
}
