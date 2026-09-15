using ECommerce.Services.Orders.Shared.Data;
using Tests.Shared.TestBase;

namespace ECommerce.Services.Orders.EndToEndTests;

[Collection(EndToEndTestCollection.Name)]
public abstract class OrdersEndToEndTestBase
    : IntegrationTestBase<Program, OrdersEndToEndSharedFixture>
{
    protected OrdersEndToEndTestBase(OrdersEndToEndSharedFixture sharedFixture)
        : base(sharedFixture) { }

    protected override async Task ResetStateAsync()
    {
        await ExecuteOrdersDbContextAsync(_ => Task.CompletedTask);
    }

    protected Task ExecuteOrdersDbContextAsync(Func<OrdersDbContext, Task> action)
    {
        return ExecuteDbContextAsync(action);
    }
}
