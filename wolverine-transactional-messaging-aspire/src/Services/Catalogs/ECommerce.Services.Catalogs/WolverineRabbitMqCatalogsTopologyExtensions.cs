using BuildingBlocks.Integration.Wolverine.Abstractions;
using BuildingBlocks.Integration.Wolverine.RabbitMQ;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using Humanizer;
using Wolverine.RabbitMQ;

namespace ECommerce.Services.Catalogs;

/// <summary>
/// Catalogs' RabbitMQ topology — publishes integration events.
///
/// Demonstrates two coexisting approaches:
/// - <b>Explicit per-type</b> for <c>ProductCreatedV1</c> (Topic exchange)
/// - <b>Conventional routing</b> for <c>OrderSubmittedV1</c> (auto-discovered)
///
/// The <c>IncludeTypes</c> filter excludes <c>ProductCreatedV1</c> from the
/// convention to avoid duplicate publisher declarations.
/// </summary>
public static class WolverineRabbitMqCatalogsTopologyExtensions
{
    /// <summary>
    /// Configures Catalogs as publisher with snake_case naming.
    /// </summary>
    public static WolverineRabbitMqRegistrationBuilder ConfigureCatalogsPublishTopology(
        this WolverineRabbitMqRegistrationBuilder builder
    )
    {
        // ── Explicit: ProductCreatedV1 → Topic exchange ─────────────
        // Routing key pattern: product_created_v1 (matches listener binding).

        builder.PublishToExchange<MessageEnvelope<ProductCreatedV1>>(
            nameof(ProductCreatedV1).Underscore()
        );
        builder.DeclareExchange(
            nameof(ProductCreatedV1).Underscore(),
            ex => ex.ExchangeType = ExchangeType.Topic
        );

        // ── Conventional routing: handles OrderSubmittedV1 ──────────
        // Auto-discovers IWolverineMessageEnvelope types and creates
        // topic exchanges with snake_case naming.
        // Explicitly excludes ProductCreatedV1 to avoid duplicate publisher.

        builder.UseSnakeCaseConventions(conventions =>
        {
            conventions.IncludeTypes(type =>
                typeof(IWolverineMessageEnvelope).IsAssignableFrom(type)
                && !(
                    type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(MessageEnvelope<>)
                    && type.GetGenericArguments()[0] == typeof(ProductCreatedV1)
                )
            );
            conventions.ConfigureSending((ex, _) => ex.ExchangeType(ExchangeType.Topic));
        });

        return builder;
    }
}
