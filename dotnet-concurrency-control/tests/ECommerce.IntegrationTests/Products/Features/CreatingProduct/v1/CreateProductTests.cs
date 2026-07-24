using System.Net;
using System.Net.Http.Json;

namespace ECommerce.IntegrationTests.Products.Features.CreatingProduct.v1;

public sealed class CreateProductTests(ECommerceSharedFixture sharedFixture)
    : ECommerceIntegrationTestBase(sharedFixture)
{
    // ── Approach: Baseline CREATE (no concurrency involved yet) ──
    // Validates product creation via API, checks HTTP 201, response body,
    // and DB persistence. Confirms Version starts at 1 and all fields
    // (Name, Stock, Price) are stored correctly.
    [Fact]
    public async Task PostProduct_WithValidData_Returns201Created()
    {
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await CreateProductAsync("Laptop", 50, 999.99m);

        // Assert – HTTP
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.Equal("Laptop", result.Name);
        Assert.Equal(50, result.Stock);

        // Assert – persisted in DB
        await ExecuteDbContextAsync(async db =>
        {
            var product = await db.Products.FindAsync([result.ProductId], ct);
            Assert.NotNull(product);
            Assert.Equal("Laptop", product.Name);
            Assert.Equal(50, product.Stock);
            Assert.Equal(999.99m, product.Price);
            Assert.Equal(1, product.Version);
        });
    }

    // ── Approach: Edge case — zero stock product ──
    // Validates that creating a product with stock=0 and price=0 is
    // accepted (no validation error for empty inventory).
    [Fact]
    public async Task PostProduct_WithZeroStock_Returns201Created()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CreateProductAsync("Digital Item", 0, 0m);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.Equal(0, result.Stock);
    }

    private sealed record CreateProductResponse(
        Guid ProductId,
        string Name,
        int Stock,
        decimal Price
    );
}
