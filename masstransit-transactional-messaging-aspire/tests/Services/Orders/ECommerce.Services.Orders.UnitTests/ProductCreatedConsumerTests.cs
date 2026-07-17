using ECommerce.Services.Orders.Products.Features.ConsumingProductCreated.v1;
using ECommerce.Services.Orders.Products.Models;
using ECommerce.Services.Orders.Shared.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ECommerce.Services.Orders.UnitTests;

public class ProductCreatedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldInsert_ImportedProduct_WhenMissing()
    {
        await using var dbContext = CreateDbContext();
        var consumer = new ProductCreatedConsumer(dbContext);
        var envelope = Tests.Shared.SampleData.ProductCreatedEnvelope();
        var context = Mock.Of<
            ConsumeContext<ECommerce.Services.Shared.Contracts.MessageEnvelope.MessageEnvelope<ECommerce.Services.Shared.Contracts.IntegrationEvents.ProductCreatedV1>>
        >(x => x.Message == envelope && x.CancellationToken == CancellationToken.None);

        await consumer.Consume(context);

        var imported = await dbContext.ImportedProducts.SingleAsync();

        Assert.Equal(Tests.Shared.SampleData.ProductId, imported.Id);
        Assert.Equal("catalog-001", imported.Code);
        Assert.Equal("Starter Basket", imported.Name);
        Assert.Equal(42.50m, imported.Price);
    }

    [Fact]
    public async Task Consume_ShouldUpdate_ImportedProduct_WhenAlreadyExists()
    {
        await using var dbContext = CreateDbContext();
        dbContext.ImportedProducts.Add(
            ImportedProduct.Create(
                Tests.Shared.SampleData.ProductId,
                "old-code",
                "Old Name",
                10m,
                Tests.Shared.SampleData.CreatedAtUtc.AddDays(-1)
            )
        );
        await dbContext.SaveChangesAsync();

        var consumer = new ProductCreatedConsumer(dbContext);
        var envelope = Tests.Shared.SampleData.ProductCreatedEnvelope();
        var context = Mock.Of<
            ConsumeContext<ECommerce.Services.Shared.Contracts.MessageEnvelope.MessageEnvelope<ECommerce.Services.Shared.Contracts.IntegrationEvents.ProductCreatedV1>>
        >(x => x.Message == envelope && x.CancellationToken == CancellationToken.None);

        await consumer.Consume(context);

        var imported = await dbContext.ImportedProducts.SingleAsync();

        Assert.Equal("catalog-001", imported.Code);
        Assert.Equal("Starter Basket", imported.Name);
        Assert.Equal(42.50m, imported.Price);
        Assert.Equal(Tests.Shared.SampleData.CreatedAtUtc, imported.CreatedAtUtc);
    }

    private static OrdersDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new OrdersDbContext(options);
    }
}
