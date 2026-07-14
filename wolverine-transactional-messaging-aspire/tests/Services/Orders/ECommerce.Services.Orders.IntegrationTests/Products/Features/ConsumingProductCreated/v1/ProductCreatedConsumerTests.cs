using System.Net;
using System.Net.Http.Json;
using ECommerce.Services.Orders.Products.Models;
using ECommerce.Services.Orders.Shared.Data;
using ECommerce.Services.Orders.TestShared;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared.Factory;

namespace ECommerce.Services.Orders.IntegrationTests.Products.Features.ConsumingProductCreated.v1;

public class ProductCreatedConsumerTests(OrdersSharedFixture sharedFixture)
    : OrdersIntegrationTestBase(sharedFixture)
{
    protected override string MessagingTransport => "rabbitmq";

    [Fact]
    public async Task ProductCreated_ShouldPersistImportedProduct_WhenConsumedFromRabbitMq()
    {
        var message = OrdersTestData.NewProductCreatedEnvelope();

        await PublishProductCreatedAsync(Factory, message);

        var imported = await WaitForImportedProductAsync(
            Factory,
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
    public async Task ProductCreated_ShouldExposeImportedProductThroughApi_WhenConsumedFromRabbitMq()
    {
        var message = OrdersTestData.NewProductCreatedEnvelope();

        await PublishProductCreatedAsync(Factory, message);

        var importedProduct = await WaitForImportedProductResponseAsync(Factory, message.ProductId);

        Assert.NotNull(importedProduct);
        Assert.Equal(message.ProductId, importedProduct!.Id);
        Assert.Equal(message.Code, importedProduct.Code);
        Assert.Equal(message.Name, importedProduct.Name);
        Assert.Equal(message.Price, importedProduct.Price);
    }

    [Fact]
    public async Task ProductCreated_ShouldPersistImportedProduct_WhenConsumedFromKafka()
    {
        await Kafka.EnsureStartedAsync();
        using var kafkaFactory = SharedFixture.CreateFactory("kafka");

        var message = OrdersTestData.NewProductCreatedEnvelope();

        await PublishProductCreatedAsync(kafkaFactory, message);

        var imported = await WaitForImportedProductAsync(
            kafkaFactory,
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
    public async Task ProductCreated_ShouldExposeImportedProductThroughApi_WhenConsumedFromKafka()
    {
        await Kafka.EnsureStartedAsync();
        using var kafkaFactory = SharedFixture.CreateFactory("kafka");

        var message = OrdersTestData.NewProductCreatedEnvelope();

        await PublishProductCreatedAsync(kafkaFactory, message);

        var importedProduct = await WaitForImportedProductResponseAsync(
            kafkaFactory,
            message.ProductId
        );

        Assert.NotNull(importedProduct);
        Assert.Equal(message.ProductId, importedProduct!.Id);
        Assert.Equal(message.Code, importedProduct.Code);
        Assert.Equal(message.Name, importedProduct.Name);
        Assert.Equal(message.Price, importedProduct.Price);
    }

    private async Task PublishProductCreatedAsync(
        CustomWebApplicationFactory<Program> appFactory,
        ProductCreatedEnvelopeData message
    )
    {
        var envelope = message.ToEnvelope();

        await SharedFixture.PublishMessageAsync(appFactory, envelope);

        await ShouldConsume<MessageEnvelope<ProductCreatedV1>>();
    }

    private async Task<ImportedProductResult?> WaitForImportedProductResponseAsync(
        CustomWebApplicationFactory<Program> appFactory,
        Guid productId
    )
    {
        using var client = appFactory.CreateClient();
        ImportedProductResult? importedProduct = null;

        await SharedFixture.WaitUntilConditionMet(async () =>
        {
            var response = await client.GetAsync("/api/v1/orders/products");

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
        CustomWebApplicationFactory<Program> appFactory,
        Guid productId,
        TimeSpan timeout
    )
    {
        ImportedProduct? imported = null;

        await SharedFixture.WaitUntilConditionMet(
            async () =>
            {
                await using var scope = appFactory.Services.CreateAsyncScope();
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
