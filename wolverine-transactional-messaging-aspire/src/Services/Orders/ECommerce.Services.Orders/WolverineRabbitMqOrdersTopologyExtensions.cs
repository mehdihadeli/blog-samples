using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.Wolverine.RabbitMQ;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using Humanizer;

namespace ECommerce.Services.Orders;

/// <summary>
/// Orders' RabbitMQ topology — consumes integration events.
///
/// Demonstrates two coexisting approaches:
/// - <b>Explicit listener</b> for <c>ProductCreatedV1</c> (Topic exchange)
/// - <b>Conventional routing</b> for <c>OrderSubmittedV1</c> (handler auto-discovered)
///
/// The <c>IncludeTypes</c> filter excludes <c>ProductCreatedV1</c> from the
/// convention to avoid duplicate listener declarations.
/// </summary>
public static class WolverineRabbitMqOrdersTopologyExtensions
{
    /// <summary>
    /// Configures Orders as consumer with durable queues.
    /// </summary>
    public static WolverineRabbitMqRegistrationBuilder ConfigureOrdersConsumeTopology(
        this WolverineRabbitMqRegistrationBuilder builder
    )
    {
        // ── Explicit: ProductCreatedV1 listener ─────────────────────
        // Queue: product_created_v1, bound to Topic exchange.

        builder.Listen<MessageEnvelope<ProductCreatedV1>>(
            nameof(ProductCreatedV1).Underscore(),
            listener => listener.ListenerCount(1)
        );

        // ── Conventional routing: auto-discovers OrderSubmittedV1 ───
        // Creates queue order_submitted_v1 + binds to Topic exchange.
        // Excludes ProductCreatedV1 to avoid duplicate listener.

        builder.UseSnakeCaseConventions(conventions =>
        {
            conventions.IncludeTypes(type =>
                typeof(IMessageEnvelope).IsAssignableFrom(type)
                && !(
                    type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(MessageEnvelope<>)
                    && type.GetGenericArguments()[0] == typeof(ProductCreatedV1)
                )
            );
            conventions.ConfigureListeners((listener, _) => listener.ListenerCount(1));
        });

        return builder;
    }
}
