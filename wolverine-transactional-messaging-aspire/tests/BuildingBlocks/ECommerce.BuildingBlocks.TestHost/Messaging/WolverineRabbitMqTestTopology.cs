using BuildingBlocks.Core.Messages;
using BuildingBlocks.Integration.Wolverine.RabbitMQ;
using ECommerce.BuildingBlocks.TestHost.Messages;
using Wolverine.RabbitMQ;

namespace ECommerce.BuildingBlocks.TestHost.Messaging;

/// <summary>
/// Manual RabbitMQ topology that exercises the building block's builder API:
/// <c>PublishToExchange</c>, <c>Listen</c>, <c>Publish&lt;T&gt;(queueName)</c>,
/// <c>UseSnakeCaseConventions</c>, <c>DeclareExchange</c>, <c>DeclareQueue</c>,
/// <c>BindQueue</c> and <c>BindExchangeToExchange</c>.
/// Wired in via <c>AddWolverineRabbitMq(..., configure: ...)</c> when
/// <c>AutoConfigMessagesTopology = false</c>.
/// </summary>
public static class WolverineRabbitMqTestTopology
{
    public const string ProductCreatedExchange = "product_created_v1";
    public const string ProductCreatedQueue = "product_created_v1";

    public const string OrderCreatedQueue = "order_created_v1";

    public const string PaymentEventsExchange = "payment_events";
    public const string PaymentProcessedQueue = "payment_processed_queue";
    public const string PaymentProcessedRoutingKey = "payment.processed";

    public const string AuditEventsExchange = "audit_events";

    public static WolverineRabbitMqRegistrationBuilder ConfigureTestRabbitMqTopology(
        this WolverineRabbitMqRegistrationBuilder builder
    )
    {
        // ── 1. Explicit Topic exchange round-trip ────────────────────
        // PublishToExchange + DeclareExchange(Topic) + Listen.
        // The exchange name doubles as the routing key, matching the
        // listener binding (queue.BindExchange(exchange, exchange)).
        builder.PublishToExchange<MessageEnvelope<ProductCreatedV1>>(ProductCreatedExchange);
        builder.DeclareExchange(
            ProductCreatedExchange,
            exchange => exchange.ExchangeType = ExchangeType.Topic
        );
        builder.Listen<MessageEnvelope<ProductCreatedV1>>(
            ProductCreatedQueue,
            listener => listener.ListenerCount(1)
        );

        // ── 2. Explicit direct-queue round-trip ──────────────────────
        // Publish<T>(queueName) + Listen<T>(queueName).
        builder.Publish<MessageEnvelope<OrderCreatedV1>>(OrderCreatedQueue);
        builder.Listen<MessageEnvelope<OrderCreatedV1>>(OrderCreatedQueue);

        // ── 3. Conventional routing (snake_case) ─────────────────────
        // InventoryAdjustedV1 → fanout exchange inventory_adjusted_v1 +
        // durable queue + binding. Sending is routed on demand at runtime.
        builder.UseSnakeCaseConventions(conventions =>
        {
            // Exclude the explicitly wired types to avoid duplicate listeners.
            conventions.IncludeTypes(type =>
                typeof(IMessageEnvelope).IsAssignableFrom(type)
                && !(
                    type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(MessageEnvelope<>)
                    && type.GetGenericArguments()[0] is var inner
                    && (inner == typeof(ProductCreatedV1) || inner == typeof(OrderCreatedV1))
                )
            );
            conventions.ConfigureListeners((listener, _) => listener.ListenerCount(1));
        });

        // ── 4. Declarative topology ──────────────────────────────────
        // DeclareExchange + DeclareQueue + BindQueue + BindExchangeToExchange.
        builder.DeclareExchange(
            PaymentEventsExchange,
            exchange => exchange.ExchangeType = ExchangeType.Topic
        );
        builder.DeclareQueue(PaymentProcessedQueue);
        builder.BindQueue(PaymentProcessedQueue, PaymentEventsExchange, PaymentProcessedRoutingKey);

        builder.DeclareExchange(
            AuditEventsExchange,
            exchange => exchange.ExchangeType = ExchangeType.Fanout
        );
        builder.BindExchangeToExchange(PaymentEventsExchange, AuditEventsExchange);

        return builder;
    }
}
