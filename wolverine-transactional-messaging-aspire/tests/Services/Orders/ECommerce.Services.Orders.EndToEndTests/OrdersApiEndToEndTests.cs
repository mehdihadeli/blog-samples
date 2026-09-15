using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace ECommerce.Services.Orders.EndToEndTests;

public sealed class OrdersApiEndToEndTests(OrdersEndToEndSharedFixture sharedFixture)
    : OrdersEndToEndTestBase(sharedFixture)
{
    [Fact]
    public async Task root_endpoint_should_report_running_service()
    {
        var response = await SharedFixture.GuestClient.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceStatus>();
        body.ShouldNotBeNull();
        body!.Status.ShouldBe("running");
    }

    [Fact]
    public async Task imported_products_endpoint_should_return_json_collection()
    {
        var response = await SharedFixture.GuestClient.GetAsync(
            "/api/v1/orders/products",
            TestCancellationToken
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<Product[]>(TestCancellationToken);
        products.ShouldNotBeNull();
    }

    private sealed record ServiceStatus(string Service, string Status);

    private sealed record Product(
        Guid Id,
        string Code,
        string Name,
        decimal Price,
        DateTime ReceivedAtUtc
    );
}
