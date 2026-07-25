using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderSaga.Orders.Features.CreatingOrder.v1;
using OrderSaga.Orders.Models;
using OrderSaga.Shared.Data;
using Shouldly;

namespace OrderSaga.IntegrationTests;

public sealed class CreateOrderTests(OrderSagaSharedFixture sharedFixture)
    : OrderSagaIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task health_check_should_return_ok()
    {
        // Act
        var response = await Factory.CreateClient().GetAsync("/");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task create_order_via_api_should_trigger_saga_and_persist_pending_order()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange
        var request = new CreateOrderRequest("John Doe", 150.00m);

        // Act — POST to trigger saga Start
        var response = await Factory.CreateClient().PostAsJsonAsync(
            "/api/v1/orders", request, ct);

        // Assert — HTTP
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Assert — order persisted with Pending status
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var orders = await db.Orders.ToListAsync(ct);
            orders.Count.ShouldBe(1);

            var order = orders[0];
            order.CustomerName.ShouldBe("John Doe");
            order.Total.ShouldBe(150.00m);
            order.Status.ShouldBe(OrderStatus.Pending);
        });
    }

    [Fact]
    public async Task create_order_should_persist_order_in_database()
    {
        // Arrange
        var customerName = "John Doe";
        var total = 150.00m;

        // Act - persist order directly via db context
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var order = Order.Create(customerName, total);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        });

        // Assert
        await ExecuteDbContextAsync<OrderDbContext>(async db =>
        {
            var orders = await db.Orders.ToListAsync();
            orders.Count.ShouldBe(1);
            orders[0].CustomerName.ShouldBe(customerName);
            orders[0].Total.ShouldBe(total);
            orders[0].Status.ShouldBe(OrderStatus.Pending);
        });
    }
}
