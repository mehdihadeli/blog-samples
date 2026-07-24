using System.Net;
using System.Net.Http.Json;
using ECommerce.Services.Orders.Products.Models;

namespace ECommerce.Services.Orders.IntegrationTests.Products.Features.GettingImportedProducts.v1;

public class GetImportedProductsTests(OrdersSharedFixture sharedFixture)
    : OrdersIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task GetProducts_ShouldReturnSeededImportedProduct()
    {
        var productId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc);

        await ExecuteOrdersDbContextAsync(async dbContext =>
        {
            dbContext.ImportedProducts.Add(
                ImportedProduct.Create(
                    productId,
                    "catalog-201",
                    "Imported Basket",
                    22.40m,
                    createdAt
                )
            );
            await dbContext.SaveChangesAsync();
        });

        var response = await SharedFixture.GuestClient.GetAsync("/api/v1/orders/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<ImportedProductResult>>();
        Assert.NotNull(products);
        Assert.Single(products!);
        Assert.Equal(productId, products[0].Id);
        Assert.Equal("catalog-201", products[0].Code);
        Assert.Equal("Imported Basket", products[0].Name);
        Assert.Equal(22.40m, products[0].Price);
    }

    private sealed record ImportedProductResult(
        Guid Id,
        string Code,
        string Name,
        decimal Price,
        DateTime ReceivedAtUtc
    );
}
