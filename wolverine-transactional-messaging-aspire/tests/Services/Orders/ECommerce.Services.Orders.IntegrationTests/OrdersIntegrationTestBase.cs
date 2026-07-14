using ECommerce.Services.Orders.Shared.Data;
using Tests.Shared.Factory;
using Tests.Shared.TestBase;

namespace ECommerce.Services.Orders.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public abstract class OrdersIntegrationTestBase : IntegrationTestBase<Program, OrdersSharedFixture>
{
    protected OrdersIntegrationTestBase(OrdersSharedFixture sharedFixture)
        : base(sharedFixture) { }

    protected override string MessagingTransport => "kafka";

    protected Task ExecuteOrdersDbContextAsync(Func<OrdersDbContext, Task> action)
    {
        return ExecuteDbContextAsync(action);
    }

    protected Task<TResult> ExecuteOrdersDbContextAsync<TResult>(
        Func<OrdersDbContext, Task<TResult>> action
    )
    {
        return ExecuteDbContextAsync<OrdersDbContext, TResult>(action);
    }
}
