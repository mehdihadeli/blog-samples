using ECommerce.Services.Orders.Products.Models;
using ECommerce.Services.Orders.TestShared;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Orders.IntegrationTests.Products.Features.GettingImportedProducts.v1;

public class GetImportedProductsTests(OrdersSharedFixture sharedFixture)
    : OrdersIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task GetProducts_ShouldReturnSeededImportedProduct()
    {
        var product = OrdersTestData.NewImportedProduct();
        await ExecuteOrdersDbContextAsync(async dbContext =>
        {
            dbContext.ImportedProducts.Add(
                ImportedProduct.Create(
                    product.ProductId,
                    product.Code,
                    product.Name,
                    product.Price,
                    product.CreatedAtUtc
                )
            );
            await dbContext.SaveChangesAsync();
        });

        var products = await ExecuteOrdersDbContextAsync(dbContext =>
            dbContext
                .ImportedProducts.OrderBy(product => product.Name)
                .ToListAsync(TestCancellationToken)
        );

        Assert.Single(products);
        Assert.Equal(product.ProductId, products[0].Id);
        Assert.Equal(product.Code, products[0].Code);
        Assert.Equal(product.Name, products[0].Name);
        Assert.Equal(product.Price, products[0].Price);
    }
}
