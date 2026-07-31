using System.Reflection;
using BuildingBlocks.Core.Messages;
using Humanizer;

namespace BuildingBlocks.Integration.Wolverine.Kafka.Extensions;

/// <summary>
/// Auto-configuration extensions for Wolverine Kafka topology.
///
/// OPTION 2 — Convention-driven auto-scan:
/// When <c>AutoConfigMessagesTopology</c> is enabled, scans the provided
/// assemblies for <c>IIntegrationEvent</c> types and automatically creates
/// Kafka publish topics / consumer listeners with snake_case naming —
/// no per-service topology files needed.
///
/// OPTION 1 — Manual per-service topology (default):
/// Use the per-service <c>configure</c> callback in <c>AddWolverineKafka</c>
/// to define topology explicitly (e.g. <c>ConfigureCatalogsPublishTopology()</c>).
/// </summary>
public static class WolverineKafkaConventionExtensions
{
    /// <summary>
    /// Applies convention-driven publish topology for all discovered
    /// <c>IIntegrationEvent</c> types in the given <paramref name="assemblies"/>.
    ///
    /// For each found integration event type T:
    /// <list type="bullet">
    ///   <item>Derives a Kafka topic name via snake_case (e.g. ProductCreatedV1 → product_created_v1)</item>
    ///   <item>Configures publish routing for <c>MessageEnvelope&lt;T&gt;</c> to that topic</item>
    /// </list>
    ///
    /// Referenced assemblies of each provided assembly are also scanned
    /// transitively to find events defined in shared contract libraries.
    /// </summary>
    public static WolverineKafkaRegistrationBuilder ApplyMessagesPublishTopology(
        this WolverineKafkaRegistrationBuilder builder,
        IReadOnlyCollection<Assembly> assemblies
    )
    {
        builder.UseSnakeCaseConventions();

        var eventTypes = assemblies
            .SelectMany(a => a.GetReferencedAssemblies().Select(Assembly.Load).Append(a))
            .Distinct()
            .SelectMany(a => SafeGetExportedTypes(a))
            .Where(t =>
                typeof(IIntegrationEvent).IsAssignableFrom(t)
                && t is { IsInterface: false, IsAbstract: false }
            )
            .Distinct()
            .ToList();

        foreach (var eventType in eventTypes)
        {
            var envelopeType = typeof(MessageEnvelope<>).MakeGenericType(eventType);
            var topicName = eventType.Name.Underscore();

            var publishMethod = typeof(WolverineKafkaRegistrationBuilder)
                .GetMethod(nameof(WolverineKafkaRegistrationBuilder.PublishToTopic))!
                .MakeGenericMethod(envelopeType);

            publishMethod.Invoke(builder, [topicName, null]);
        }

        return builder;
    }

    /// <summary>
    /// Applies convention-driven consume topology for all discovered
    /// <c>IIntegrationEvent</c> types in the given <paramref name="assemblies"/>.
    ///
    /// For each found integration event type T:
    /// <list type="bullet">
    ///   <item>Creates a Kafka consumer listening to the snake_case topic name</item>
    ///   <item>Consumer group is auto-derived from the event type name via the naming convention</item>
    /// </list>
    ///
    /// Referenced assemblies of each provided assembly are also scanned
    /// transitively to find events defined in shared contract libraries.
    /// </summary>
    public static WolverineKafkaRegistrationBuilder ApplyMessagesConsumeTopology(
        this WolverineKafkaRegistrationBuilder builder,
        IReadOnlyCollection<Assembly> assemblies
    )
    {
        builder.UseSnakeCaseConventions();

        var eventTypes = assemblies
            .SelectMany(a => a.GetReferencedAssemblies().Select(Assembly.Load).Append(a))
            .Distinct()
            .SelectMany(a => SafeGetExportedTypes(a))
            .Where(t =>
                typeof(IIntegrationEvent).IsAssignableFrom(t)
                && t is { IsInterface: false, IsAbstract: false }
            )
            .Distinct()
            .ToList();

        foreach (var eventType in eventTypes)
        {
            var envelopeType = typeof(MessageEnvelope<>).MakeGenericType(eventType);

            // Uses Listen<T>() which auto-derives topic + consumer group from naming convention
            var listenMethod = typeof(WolverineKafkaRegistrationBuilder)
                .GetMethods()
                .First(m =>
                    m.Name == nameof(WolverineKafkaRegistrationBuilder.Listen)
                    && m.GetParameters().Length == 1
                )
                .MakeGenericMethod(envelopeType);

            listenMethod.Invoke(builder, [null]);
        }

        return builder;
    }

    private static IEnumerable<Type> SafeGetExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch
        {
            return [];
        }
    }
}
