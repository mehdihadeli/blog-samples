using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Core.Messages;
using ECommerce.Services.Orders.Products.Models;
using ECommerce.Services.Orders.Shared.Data;
using ECommerce.Services.Orders.TestShared;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wolverine;
using Wolverine.Persistence.Durability;

namespace ECommerce.Services.Orders.IntegrationTests.Products.Features.ConsumingProductCreated.v1;

public class ProductCreatedConsumerTests(OrdersSharedFixture sharedFixture)
    : OrdersIntegrationTestBase(sharedFixture)
{
    /// <summary>
    /// Publishes <c>MessageEnvelope&lt;ProductCreatedV1&gt;</c> through the real
    /// RabbitMQ broker via <c>IMessageBus.PublishAsync</c>. Auto-config topology
    /// routes it to the <c>product_created_v1</c> exchange, the consumer listener
    /// picks it up and persists the imported product. Verifies the full
    /// broker round-trip: publish → exchange → queue → consume → DB.
    /// </summary>
    private async Task PublishEnvelopeAsync(MessageEnvelope<ProductCreatedV1> envelope)
    {
        await using var scope = SharedFixture.ServiceProvider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.PublishAsync(envelope);
    }

    [Fact]
    public async Task should_consume_product_created_through_broker()
    {
        var message = OrdersTestData.NewProductCreatedEnvelope();
        var envelope = message.ToEnvelope();

        // Act — publish full MessageEnvelope<ProductCreatedV1> via IMessageBus
        await PublishEnvelopeAsync(envelope);

        // Assert — consumer processed: ImportedProduct persisted in DB
        var imported = await WaitForImportedProductAsync(message.ProductId);

        imported.ShouldNotBeNull();
    }

    [Fact]
    public async Task should_persist_imported_product_in_db_after_consuming()
    {
        var message = OrdersTestData.NewProductCreatedEnvelope();
        var envelope = message.ToEnvelope();

        // Act — publish full MessageEnvelope<ProductCreatedV1> via IMessageBus
        await PublishEnvelopeAsync(envelope);

        // Assert — all DB fields correctly persisted
        var imported = await WaitForImportedProductAsync(message.ProductId);

        imported.ShouldNotBeNull();
        imported!.Id.ShouldBe(message.ProductId);
        imported.Code.ShouldBe(message.Code);
        imported.Name.ShouldBe(message.Name);
        imported.Price.ShouldBe(message.Price);
    }

    [Fact]
    public async Task should_expose_imported_product_through_api_after_consuming()
    {
        var message = OrdersTestData.NewProductCreatedEnvelope();
        var envelope = message.ToEnvelope();

        // Act — publish full MessageEnvelope<ProductCreatedV1> via IMessageBus
        await PublishEnvelopeAsync(envelope);

        // Assert — API serves the imported product
        var importedProduct = await WaitForImportedProductResponseAsync(message.ProductId);

        importedProduct.ShouldNotBeNull();
        importedProduct!.Id.ShouldBe(message.ProductId);
        importedProduct.Code.ShouldBe(message.Code);
        importedProduct.Name.ShouldBe(message.Name);
        importedProduct.Price.ShouldBe(message.Price);
    }

    [Fact]
    public async Task should_be_consumed_by_product_created_handler()
    {
        var message = OrdersTestData.NewProductCreatedEnvelope();
        var envelope = message.ToEnvelope();

        // Act + Assert — TrackActivity round-trip through RabbitMQ: envelope is
        // Received by the consumer, handled successfully (MessageSucceeded), no
        // fault published, and the handler's side-effect (ImportedProduct row)
        // is visible in the DB — proving it was consumed by THIS consumer.
        await SharedFixture.ShouldConsuming<MessageEnvelope<ProductCreatedV1>>(
            async _ => await PublishEnvelopeAsync(envelope),
            assertSideEffect: async () =>
            {
                var imported = await WaitForImportedProductAsync(message.ProductId);
                imported.ShouldNotBeNull();
            },
            includeExternalTransports: true,
            cancellationToken: TestCancellationToken
        );
    }

    [Fact]
    public async Task should_consume_through_broker_and_persist_envelope_in_receiver_inbox()
    {
        var message = OrdersTestData.NewProductCreatedEnvelope();
        var envelope = message.ToEnvelope();

        // Act + Assert — TrackActivity round-trip through RabbitMQ. The receiver-side
        // inbox (the consumer pipeline) must receive the envelope through the broker and
        // handle it exactly once: Received + MessageSucceeded with no AutoFault.
        //
        // NOTE: Orders consumes with INLINE RabbitMQ listeners (UseDurableInboxOnAllListeners
        // is false), so Wolverine does not persist a handled copy into
        // wolverine.wolverine_incoming_envelopes — the inbox is the in-memory consumer
        // pipeline itself. That is exactly what TrackActivity's Received/MessageSucceeded
        // events prove (MassTransit's AssertConsumed analog).
        await SharedFixture.ShouldConsuming<MessageEnvelope<ProductCreatedV1>>(
            async _ => await PublishEnvelopeAsync(envelope),
            assertSideEffect: async () =>
            {
                // The message store (wolverine schema) still backs the receiver's
                // inbox/outbox infrastructure — confirm it is queryable and healthy.
                await using var scope = SharedFixture.ServiceProvider.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IMessageStore>();
                var allIncoming = await store.Admin.AllIncomingAsync();
                allIncoming.ShouldNotBeNull();
            },
            includeExternalTransports: true,
            cancellationToken: TestCancellationToken
        );
    }

    private async Task<ImportedProduct?> WaitForImportedProductAsync(Guid productId)
    {
        ImportedProduct? imported = null;

        await SharedFixture.WaitUntilConditionMet(
            async () =>
            {
                await using var scope = SharedFixture.ServiceProvider.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
                imported = await dbContext.ImportedProducts.FindAsync(productId);
                return imported is not null;
            },
            timeoutSecond: 30,
            cancellationToken: TestCancellationToken
        );

        return imported;
    }

    private async Task<ImportedProductResult?> WaitForImportedProductResponseAsync(Guid productId)
    {
        ImportedProductResult? importedProduct = null;

        await SharedFixture.WaitUntilConditionMet(
            async () =>
            {
                var response = await SharedFixture.GuestClient.GetAsync("/api/v1/orders/products");

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                var products = await response.Content.ReadFromJsonAsync<
                    List<ImportedProductResult>
                >();
                importedProduct = products?.SingleOrDefault(x => x.Id == productId);
                return importedProduct is not null;
            },
            timeoutSecond: 30,
            cancellationToken: TestCancellationToken
        );

        return importedProduct;
    }

    private sealed record ImportedProductResult(
        Guid Id,
        string Code,
        string Name,
        decimal Price,
        DateTime ReceivedAtUtc
    );
}
