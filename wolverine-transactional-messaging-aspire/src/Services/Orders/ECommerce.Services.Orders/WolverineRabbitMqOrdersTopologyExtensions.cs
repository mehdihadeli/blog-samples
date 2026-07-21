using BuildingBlocks.Integration.Wolverine.RabbitMQ;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using Humanizer;

namespace ECommerce.Services.Orders;

/// <summary>
/// Orders' RabbitMQ topology — consumes integration events.
/// Two approaches available:
/// - Option 1 (preferred, active): Explicit per-message-type Listen (matches Catalogs' explicit publish)
/// - Option 2 (commented): Wolverine conventional routing (auto-discovery, less boilerplate)
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
        // ── Option 1 (preferred): Explicit per-message-type listener ──
        // Transparent, debuggable topology. Repeat per consumed type.
        // Queue names derived via Humanizer.Underscore().

        builder.Listen<ProductCreatedV1>(
            nameof(ProductCreatedV1).Underscore(),
            listener => listener.ListenerCount(1)
        );

        // ── Option 2: Wolverine conventional routing ──
        // Auto-discovers handlers — less boilerplate
        // but topology is implicit.
        //
        // builder.UseSnakeCaseConventions(conventions =>
        // {
        //     conventions.IncludeTypes(type => typeof(IIntegrationEvent).IsAssignableFrom(type));
        //     conventions.ConfigureListeners((listener, _) => listener.ListenerCount(1));
        // });

        return builder;
    }
}
