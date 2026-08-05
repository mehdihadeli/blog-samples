using Humanizer;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ;

/// <summary>
/// Naming helpers for Wolverine's conventional routing.
/// </summary>
public static class TopologyHelper
{
    // ── Snake-case conversion ─────────────────────────────────────────

    /// <summary>
    /// Converts <c>ProductCreatedV1</c> → <c>product_created_v1</c>.
    /// Delegates to Humanizer's <c>Underscore()</c> for correct PascalCase
    /// and number-boundary handling.
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        return name.Underscore();
    }

    /// <summary>
    /// Converts a <see cref="Type"/> name to snake_case.
    /// Handles open generics: <c>MessageEnvelope&lt;ProductCreatedV1&gt;</c>
    /// becomes <c>product_created_v1</c> (inner type only).
    /// </summary>
    public static string TypeNameToSnakeCase(Type type)
    {
        var inner = UnwrapEnvelope(type);
        return ToSnakeCase(inner.Name);
    }

    // ── Envelope unwrapping ──────────────────────────────────────────

    /// <summary>
    /// If <paramref name="type"/> wraps a message type (e.g.
    /// <c>MessageEnvelope&lt;T&gt;</c> or anything implementing
    /// <c>IMessageEnvelope</c>), returns the inner message type.
    /// Otherwise returns the type unchanged.
    /// </summary>
    public static Type UnwrapEnvelope(Type type)
    {
        // Check for IMessageEnvelope with a Message property
        // (handles both MessageEnvelope<T> and any custom envelope).
        if (typeof(global::BuildingBlocks.Core.Messages.IMessageEnvelope).IsAssignableFrom(type))
        {
            var messageProp = type.GetProperty("Message");
            if (messageProp != null)
            {
                return messageProp.PropertyType;
            }
        }

        return type;
    }
}
