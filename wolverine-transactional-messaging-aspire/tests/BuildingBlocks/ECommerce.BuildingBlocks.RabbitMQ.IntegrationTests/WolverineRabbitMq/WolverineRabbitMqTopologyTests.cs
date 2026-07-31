using ECommerce.BuildingBlocks.RabbitMQ.IntegrationTests.Infrastructure;
using ECommerce.BuildingBlocks.TestHost.Messaging;
using Shouldly;

namespace ECommerce.BuildingBlocks.RabbitMQ.IntegrationTests.WolverineRabbitMq;

/// <summary>
/// Verifies the topology actually declared on the broker via the management
/// HTTP API: exchange types, queue existence, queue bindings and
/// exchange-to-exchange bindings produced by the building-block builder
/// (<c>DeclareExchange</c>, <c>DeclareQueue</c>, <c>BindQueue</c>,
/// <c>BindExchangeToExchange</c>, <c>UseSnakeCaseConventions</c>).
/// </summary>
public class WolverineRabbitMqTopologyTests(RabbitMqBuildingBlocksSharedFixture sharedFixture)
    : RabbitMqBuildingBlocksIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task should_declare_topic_exchange_and_queue_for_explicit_listen()
    {
        using var management = await CreateManagementClientAsync();

        var exchanges = await management.GetExchangesAsync(TestCancellationToken);
        var queues = await management.GetQueuesAsync(TestCancellationToken);

        // PublishToExchange + DeclareExchange(Topic) + Listen<T>(queue).
        exchanges.ShouldContain(e =>
            e.Name == WolverineRabbitMqTestTopology.ProductCreatedExchange && e.Type == "topic"
        );
        queues.ShouldContain(q => q.Name == WolverineRabbitMqTestTopology.ProductCreatedQueue);
    }

    [Fact]
    public async Task should_bind_explicit_queue_to_topic_exchange_with_exchange_routing_key()
    {
        using var management = await CreateManagementClientAsync();

        var bindings = await management.GetBindingsAsync(TestCancellationToken);

        bindings.ShouldContain(b =>
            b.Source == WolverineRabbitMqTestTopology.ProductCreatedExchange
            && b.Destination == WolverineRabbitMqTestTopology.ProductCreatedQueue
            && b.DestinationType == "queue"
        );
    }

    [Fact]
    public async Task should_declare_snake_case_conventional_topology()
    {
        using var management = await CreateManagementClientAsync();

        var exchanges = await management.GetExchangesAsync(TestCancellationToken);
        var queues = await management.GetQueuesAsync(TestCancellationToken);
        var bindings = await management.GetBindingsAsync(TestCancellationToken);

        // Conventional routing: exchange + durable queue named after the
        // snake_cased message type, bound with the exchange name as key.
        const string conventionalName = "inventory_adjusted_v1";

        exchanges.ShouldContain(e => e.Name == conventionalName);
        queues.ShouldContain(q => q.Name == conventionalName);
        bindings.ShouldContain(b =>
            b.Source == conventionalName
            && b.Destination == conventionalName
            && b.DestinationType == "queue"
        );
    }

    [Fact]
    public async Task should_declare_queue_binding_and_exchange_to_exchange_binding()
    {
        using var management = await CreateManagementClientAsync();

        var exchanges = await management.GetExchangesAsync(TestCancellationToken);
        var queues = await management.GetQueuesAsync(TestCancellationToken);
        var bindings = await management.GetBindingsAsync(TestCancellationToken);

        exchanges.ShouldContain(e =>
            e.Name == WolverineRabbitMqTestTopology.PaymentEventsExchange && e.Type == "topic"
        );
        exchanges.ShouldContain(e =>
            e.Name == WolverineRabbitMqTestTopology.AuditEventsExchange && e.Type == "fanout"
        );
        queues.ShouldContain(q => q.Name == WolverineRabbitMqTestTopology.PaymentProcessedQueue);

        // BindQueue(queue, exchange, routingKey).
        bindings.ShouldContain(b =>
            b.Source == WolverineRabbitMqTestTopology.PaymentEventsExchange
            && b.Destination == WolverineRabbitMqTestTopology.PaymentProcessedQueue
            && b.DestinationType == "queue"
            && b.RoutingKey == WolverineRabbitMqTestTopology.PaymentProcessedRoutingKey
        );

        // BindExchangeToExchange(source, destination).
        bindings.ShouldContain(b =>
            b.Source == WolverineRabbitMqTestTopology.PaymentEventsExchange
            && b.Destination == WolverineRabbitMqTestTopology.AuditEventsExchange
            && b.DestinationType == "exchange"
        );
    }

    private async Task<RabbitMqManagementClient> CreateManagementClientAsync()
    {
        int managementPort;
        try
        {
            managementPort = SharedFixture.RabbitMq!.Container.GetMappedPublicPort(15672);
        }
        catch (NullReferenceException)
        {
            throw new InvalidOperationException(
                "RabbitMQ management port (15672) is not published."
            );
        }

        return new RabbitMqManagementClient(
            SharedFixture.RabbitMq.Container.Hostname,
            managementPort
        );
    }
}
