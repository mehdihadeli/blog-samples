using BuildingBlocks.Core.Messages;
using ECommerce.BuildingBlocks.TestHost.Messages;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace ECommerce.BuildingBlocks.RabbitMQ.IntegrationTests.WolverineRabbitMq;

/// <summary>
/// Round-trip tests for the RabbitMQ building-block publish/listen APIs:
/// <c>PublishToExchange</c> (Topic exchange), <c>Publish&lt;T&gt;(queueName)</c>
/// (direct queue) and <c>UseSnakeCaseConventions</c> (conventional routing).
/// </summary>
public class WolverineRabbitMqPublishTests(RabbitMqBuildingBlocksSharedFixture sharedFixture)
    : RabbitMqBuildingBlocksIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task should_round_trip_product_created_via_topic_exchange()
    {
        var envelope = MessageEnvelopeFactory.From(NewProductCreated());

        // PublishToExchange<ProductCreatedV1>("product_created_v1") →
        // Topic exchange product_created_v1 → queue product_created_v1.
        await SharedFixture.ShouldConsuming<MessageEnvelope<ProductCreatedV1>>(
            async _ => await PublishEnvelopeAsync(envelope),
            includeExternalTransports: true,
            cancellationToken: TestCancellationToken
        );
    }

    [Fact]
    public async Task should_round_trip_order_created_via_direct_queue()
    {
        var envelope = MessageEnvelopeFactory.From(NewOrderCreated());

        // Publish<OrderCreatedV1>("order_created_v1") + Listen<OrderCreatedV1>("order_created_v1").
        await SharedFixture.ShouldConsuming<MessageEnvelope<OrderCreatedV1>>(
            async _ => await PublishEnvelopeAsync(envelope),
            includeExternalTransports: true,
            cancellationToken: TestCancellationToken
        );
    }

    [Fact]
    public async Task should_round_trip_inventory_adjusted_via_conventional_routing()
    {
        var envelope = MessageEnvelopeFactory.From(NewInventoryAdjusted());

        // Conventional routing: fanout exchange inventory_adjusted_v1 +
        // durable queue of the same name; the sending route is discovered
        // on demand at runtime.
        await SharedFixture.ShouldConsuming<MessageEnvelope<InventoryAdjustedV1>>(
            async _ => await PublishEnvelopeAsync(envelope),
            includeExternalTransports: true,
            cancellationToken: TestCancellationToken
        );
    }

    private async Task PublishEnvelopeAsync<T>(T envelope)
        where T : class
    {
        await using var scope = SharedFixture.ServiceProvider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.PublishAsync(envelope);
    }

    private static ProductCreatedV1 NewProductCreated() =>
        new(Guid.NewGuid(), "PC-001", "Test Product", 9.99m, DateTime.UtcNow);

    private static OrderCreatedV1 NewOrderCreated() => new(Guid.NewGuid(), Guid.NewGuid(), 49.99m);

    private static InventoryAdjustedV1 NewInventoryAdjusted() => new(Guid.NewGuid(), 5);
}
