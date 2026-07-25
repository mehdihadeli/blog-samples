using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Order.Orders.Features.CreatingOrder.v1;
using Shouldly;

namespace Order.IntegrationTests;

[Collection("integration-tests")]
public sealed class CreateOrderTests(OrderSharedFixture sharedFixture)
    : OrderIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task Should_Create_Order_Successfully()
    {
        // Arrange
        var request = new CreateOrderRequest("John Doe", 150.00m);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/orders", request);

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        // Verify order persisted
        await ExecuteDbContextAsync(async db =>
        {
            var order = await db.Orders.FirstOrDefaultAsync();
            order.ShouldNotBeNull();
            order.CustomerName.ShouldBe("John Doe");
            order.Total.ShouldBe(150.00m);
            order.Status.ShouldBe(Order.Orders.Models.OrderStatus.Pending);
        });
    }
}
