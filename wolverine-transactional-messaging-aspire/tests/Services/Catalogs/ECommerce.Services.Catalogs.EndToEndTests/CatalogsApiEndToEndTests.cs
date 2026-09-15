using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace ECommerce.Services.Catalogs.EndToEndTests;

public sealed class CatalogsApiEndToEndTests(CatalogsEndToEndSharedFixture sharedFixture)
    : CatalogsEndToEndTestBase(sharedFixture)
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
    public async Task product_should_be_created_and_retrieved_through_public_api()
    {
        var request = new CreateProductRequest(
            $"E2E-{Guid.NewGuid():N}",
            "End-to-end product",
            12.50m
        );

        var createResponse = await SharedFixture.GuestClient.PostAsJsonAsync(
            "/api/v1/catalogs/products",
            request,
            TestCancellationToken
        );

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedProduct>(
            TestCancellationToken
        );
        created.ShouldNotBeNull();
        createResponse.Headers.Location.ShouldNotBeNull();

        var getResponse = await SharedFixture.GuestClient.GetAsync(
            createResponse.Headers.Location,
            TestCancellationToken
        );

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var product = await getResponse.Content.ReadFromJsonAsync<Product>(TestCancellationToken);
        product.ShouldNotBeNull();
        product!.Id.ShouldBe(created!.Id);
        product.Code.ShouldBe(request.Code);
        product.Name.ShouldBe(request.Name);
        product.Price.ShouldBe(request.Price);
    }

    private sealed record ServiceStatus(string Service, string Status);

    private sealed record CreateProductRequest(string Code, string Name, decimal Price);

    private sealed record CreatedProduct(Guid Id, string Code, string Name, decimal Price);

    private sealed record Product(
        Guid Id,
        string Code,
        string Name,
        decimal Price,
        DateTime CreatedAtUtc
    );
}
