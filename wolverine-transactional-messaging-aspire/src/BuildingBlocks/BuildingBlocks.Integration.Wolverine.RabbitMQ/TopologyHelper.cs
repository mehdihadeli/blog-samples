using System.Text.RegularExpressions;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ;

/// <summary>
/// Naming helpers for Wolverine's conventional routing.
/// </summary>
public static partial class TopologyHelper
{
    // ── Snake-case conversion ─────────────────────────────────────────

    /// <summary>
    /// Converts <c>ProductCreatedV1</c> → <c>product_created_v1</c>.
    /// Lowercase with underscores at Pascal boundaries and number transitions.
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        // Insert underscore before uppercase letters followed by lowercase,
        // and before digits preceded by letters (or vice versa).
        var s = SnakeCaseBoundaryRegex().Replace(name, "_$1_$2");
        s = UppercaseBeforeLowercaseRegex().Replace(s, "_$1");
        s = s.Trim('_').ToLowerInvariant();

        // Collapse consecutive underscores
        return CollapseUnderscoresRegex().Replace(s, "_");
    }

    [GeneratedRegex("([a-z])([A-Z0-9])")]
    private static partial Regex SnakeCaseBoundaryRegex();

    [GeneratedRegex("([A-Z]+)([A-Z][a-z])")]
    private static partial Regex UppercaseBeforeLowercaseRegex();

    [GeneratedRegex("_+")]
    private static partial Regex CollapseUnderscoresRegex();

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
    /// <c>IWolverineMessageEnvelope</c>), returns the inner message type.
    /// Otherwise returns the type unchanged.
    /// </summary>
    public static Type UnwrapEnvelope(Type type)
    {
        // Check for IWolverineMessageEnvelope with a Message property
        // (handles both MessageEnvelope<T> and any custom envelope).
        if (typeof(Abstractions.IWolverineMessageEnvelope).IsAssignableFrom(type))
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
