using BuildingBlocks.Core.Messages;
using ECommerce.BuildingBlocks.TestHost.Messages;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace ECommerce.BuildingBlocks.Kafka.IntegrationTests.WolverineKafka;

/// <summary>
/// Round-trip tests for the Kafka building-block publish/listen APIs:
/// <c>PublishToTopic</c> (explicit topic), <c>UseSnakeCaseConventions</c>
/// (auto-named topic/group) and <c>WithNamingConvention</c> (custom naming).
/// Every listener uses earliest offset (AutoOffsetReset.Earliest) so cold-start
/// consumer groups pick up messages produced after topic re-creation by cleanup.
/// </summary>
public class WolverineKafkaPublishTests(KafkaBuildingBlocksSharedFixture sharedFixture)
    : KafkaBuildingBlocksIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task should_round_trip_product_created_via_explicit_topic()
    {
        var envelope = MessageEnvelopeFactory.From(NewProductCreated());

        // PublishToTopic<ProductCreatedV1>("catalogs-products-created", spec) +
        // Listen<ProductCreatedV1>("catalogs-products-created", "orders-products").
        await SharedFixture.ShouldConsuming<MessageEnvelope<ProductCreatedV1>>(
            async _ => await PublishEnvelopeAsync(envelope),
            includeExternalTransports: true,
            cancellationToken: TestCancellationToken
        );
    }

    [Fact]
    public async Task should_round_trip_order_created_via_auto_named_snake_case_topic()
    {
        var envelope = MessageEnvelopeFactory.From(NewOrderCreated());

        // Publish<OrderCreatedV1>() + Listen<OrderCreatedV1>() →
        // auto topic/group "order_created_v1".
        await SharedFixture.ShouldConsuming<MessageEnvelope<OrderCreatedV1>>(
            async _ => await PublishEnvelopeAsync(envelope),
            includeExternalTransports: true,
            cancellationToken: TestCancellationToken
        );
    }

    [Fact]
    public async Task should_round_trip_inventory_adjusted_via_custom_naming_convention()
    {
        var envelope = MessageEnvelopeFactory.From(NewInventoryAdjusted());

        // WithNamingConvention("custom-" + snake_case) →
        // topic "custom-inventory_adjusted_v1".
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
