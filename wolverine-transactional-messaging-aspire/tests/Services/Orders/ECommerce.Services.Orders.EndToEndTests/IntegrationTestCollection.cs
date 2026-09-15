namespace ECommerce.Services.Orders.EndToEndTests;

[CollectionDefinition(Name)]
public sealed class EndToEndTestCollection : ICollectionFixture<OrdersEndToEndSharedFixture>
{
    public const string Name = "orders-end-to-end-tests";
}
