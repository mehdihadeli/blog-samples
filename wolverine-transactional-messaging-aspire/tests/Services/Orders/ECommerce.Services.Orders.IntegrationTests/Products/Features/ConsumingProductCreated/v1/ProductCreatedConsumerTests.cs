using System.Net;
using System.Net.Http.Json;
using ECommerce.Services.Orders.Products.Models;
using ECommerce.Services.Orders.Shared.Data;
using ECommerce.Services.Orders.TestShared;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Services.Orders.IntegrationTests.Products.Features.ConsumingProductCreated.v1;

public class ProductCreatedConsumerTests(OrdersSharedFixture sharedFixture)
    : OrdersIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task ProductCreated_ShouldPersistImportedProduct_WhenConsumed()
    {
        var message = OrdersTestData.NewProductCreatedEnvelope();

        await PublishProductCreatedAsync(message);

        var imported = await WaitForImportedProductAsync(
            message.ProductId,
            TimeSpan.FromSeconds(30)
        );

        Assert.NotNull(imported);
        Assert.Equal(message.ProductId, imported!.Id);
        Assert.Equal(message.Code, imported.Code);
        Assert.Equal(message.Name, imported.Name);
        Assert.Equal(message.Price, imported.Price);
    }

    [Fact]
    public async Task ProductCreated_ShouldExposeImportedProductThroughApi_WhenConsumed()
    {
        var message = OrdersTestData.NewProductCreatedEnvelope();

        await PublishProductCreatedAsync(message);

        var importedProduct = await WaitForImportedProductResponseAsync(message.ProductId);

        Assert.NotNull(importedProduct);
        Assert.Equal(message.ProductId, importedProduct!.Id);
        Assert.Equal(message.Code, importedProduct.Code);
        Assert.Equal(message.Name, importedProduct.Name);
        Assert.Equal(message.Price, importedProduct.Price);
    }

    private async Task PublishProductCreatedAsync(ProductCreatedEnvelopeData message)
    {
        var envelope = message.ToEnvelope();

        await SharedFixture.PublishMessageAsync(envelope);

        await SharedFixture.ShouldConsuming<MessageEnvelope<ProductCreatedV1>>();
    }

    private async Task<ImportedProductResult?> WaitForImportedProductResponseAsync(Guid productId)
    {
        ImportedProductResult? importedProduct = null;

        await SharedFixture.WaitUntilConditionMet(async () =>
        {
            var response = await SharedFixture.GuestClient.GetAsync("/api/v1/orders/products");

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            var products = await response.Content.ReadFromJsonAsync<List<ImportedProductResult>>();
            importedProduct = products?.SingleOrDefault(x => x.Id == productId);
            return importedProduct is not null;
        });

        return importedProduct;
    }

    private async Task<ImportedProduct?> WaitForImportedProductAsync(
        Guid productId,
        TimeSpan timeout
    )
    {
        ImportedProduct? imported = null;

        await SharedFixture.WaitUntilConditionMet(
            async () =>
            {
                await using var scope = SharedFixture.ServiceProvider.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

                imported = await dbContext.ImportedProducts.FindAsync([productId]);

                return imported is not null;
            },
            (int)timeout.TotalSeconds
        );

        return imported;
    }

    private sealed record ImportedProductResult(
        Guid Id,
        string Code,
        string Name,
        decimal Price,
        DateTime ReceivedAtUtc
    );
}
