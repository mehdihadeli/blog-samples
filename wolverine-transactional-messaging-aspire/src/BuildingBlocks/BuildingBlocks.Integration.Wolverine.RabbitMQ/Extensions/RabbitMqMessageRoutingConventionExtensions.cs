using Wolverine.RabbitMQ;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ.Extensions;

/// <summary>
/// Snake-case naming extension for
/// <see cref="RabbitMqMessageRoutingConvention"/>.
/// </summary>
public static class RabbitMqMessageRoutingConventionExtensions
{
    /// <summary>
    /// Sets exchange names, queue names, and broker identifiers
    /// to snake_case based on the inner message type (unwrapping
    /// <c>MessageEnvelope&lt;T&gt;</c> etc.).
    /// </summary>
    public static RabbitMqMessageRoutingConvention UseSnakeCaseNaming(
        this RabbitMqMessageRoutingConvention convention
    )
    {
        // Exchange name: product_created_v1
        convention.ExchangeNameForSending(TopologyHelper.TypeNameToSnakeCase);

        // Queue name: product_created_v1
        convention.QueueNameForListener(TopologyHelper.TypeNameToSnakeCase);

        // Broker identifiers (used internally by Wolverine)
        convention.IdentifierForSender(TopologyHelper.TypeNameToSnakeCase);

        convention.IdentifierForListener(TopologyHelper.TypeNameToSnakeCase);

        return convention;
    }
}
