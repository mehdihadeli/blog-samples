using Xunit;

namespace ECommerce.Services.Orders.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<OrdersSharedFixture>
{
    public const string Name = "orders-integration-tests";
}
