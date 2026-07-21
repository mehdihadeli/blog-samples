using BuildingBlocks.Integration.Wolverine.RabbitMQ;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using Humanizer;
using Wolverine.RabbitMQ;

namespace ECommerce.Services.Catalogs;

/// <summary>
/// Catalogs' RabbitMQ topology — publishes integration events.
/// Two approaches available:
/// - Option 1 (preferred, active): Explicit per-message-type binding (MassTransit attached sample style)
/// - Option 2 (commented): Wolverine conventional routing (auto-discovery, less boilerplate)
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
        // ── Option 1 (preferred): Explicit per-message-type topology ──
        // Transparent, debuggable topology. Repeat per message type.
        // Matches MassTransit attached sample (Message/Publish/Send per type).
        // Names derived via Humanizer.Underscore() — same as attached sample.

        builder.PublishToExchange<ProductCreatedV1>(nameof(ProductCreatedV1).Underscore());
        builder.DeclareExchange(
            nameof(ProductCreatedV1).Underscore(),
            ex => ex.ExchangeType = ExchangeType.Direct
        );

        // ── Option 2: Wolverine conventional routing ──
        // Auto-discovers IIntegrationEvent types — less boilerplate
        // but topology is implicit (harder to trace).
        //
        // builder.UseSnakeCaseConventions(conventions =>
        // {
        //     conventions.IncludeTypes(type => typeof(IIntegrationEvent).IsAssignableFrom(type));
        //     conventions.ConfigureSending((ex, _) => ex.ExchangeType(ExchangeType.Direct));
        // });

        return builder;
    }
}
