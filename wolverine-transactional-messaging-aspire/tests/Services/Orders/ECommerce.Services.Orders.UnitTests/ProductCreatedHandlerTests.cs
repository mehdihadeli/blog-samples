using ECommerce.Services.Orders.Products.Features.ConsumingProductCreated.v1;
using ECommerce.Services.Orders.Shared.Data;
using ECommerce.Services.Orders.TestShared;
using Microsoft.EntityFrameworkCore;
using Tests.Shared;

namespace ECommerce.Services.Orders.UnitTests;

public class ProductCreatedHandlerTests
{
    [Fact]
    public async Task Handle_ShouldInsert_ImportedProduct_WhenMissing()
    {
        await using var dbContext = CreateDbContext();

        await ProductCreatedHandler.Handle(
            SampleData.ProductCreatedEnvelope(),
            dbContext,
            CancellationToken.None
        );

        var imported = await dbContext.ImportedProducts.SingleAsync();

        Assert.Equal(OrdersTestData.ExistingProductId, imported.Id);
        Assert.Equal("catalog-001", imported.Code);
        Assert.Equal("Starter Basket", imported.Name);
        Assert.Equal(42.50m, imported.Price);
    }

    [Fact]
    public async Task Handle_ShouldUpdate_ImportedProduct_WhenAlreadyExists()
    {
        await using var dbContext = CreateDbContext();
        dbContext.ImportedProducts.Add(
            Products.Models.ImportedProduct.Create(
                OrdersTestData.ExistingProductId,
                "old-code",
                "Old Name",
                10m,
                SampleData.CreatedAtUtc.AddDays(-1)
            )
        );
        await dbContext.SaveChangesAsync();

        await ProductCreatedHandler.Handle(
            SampleData.ProductCreatedEnvelope(),
            dbContext,
            CancellationToken.None
        );

        var imported = await dbContext.ImportedProducts.SingleAsync();

        Assert.Equal("catalog-001", imported.Code);
        Assert.Equal("Starter Basket", imported.Name);
        Assert.Equal(42.50m, imported.Price);
        Assert.Equal(SampleData.CreatedAtUtc, imported.SourceCreatedAtUtc);
    }

    private static OrdersDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new OrdersDbContext(options);
    }
}
