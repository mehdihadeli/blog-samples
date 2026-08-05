using System.Reflection;
using BuildingBlocks.Core.Messages;
using Wolverine.RabbitMQ;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ.Extensions;

/// <summary>
/// Auto-configuration extensions for Wolverine RabbitMQ topology.
///
/// OPTION 2 — Convention-driven auto-scan:
/// When <c>AutoConfigMessagesTopology</c> is enabled, scans the provided
/// assemblies for <c>IIntegrationEvent</c> types and automatically creates
/// topic exchanges with snake_case naming for publishers, plus conventional
/// routing for handler-discovered consumers — no per-service topology files needed.
///
/// OPTION 1 — Manual per-service topology (default):
/// Use the per-service <c>configure</c> callback in <c>AddWolverineRabbitMq</c>
/// to define topology explicitly (e.g. <c>ConfigureCatalogsPublishTopology()</c>).
/// </summary>
public static class WolverineRabbitMqConventionExtensions
{
    /// <summary>
    /// Applies convention-driven publish topology for all discovered
    /// <c>IIntegrationEvent</c> types in the given <paramref name="assemblies"/>.
    /// Also activates snake_case conventional routing for handler topology.
    ///
    /// For each found integration event type T:
    /// <list type="bullet">
    ///   <item>Declares a Topic exchange named after the inner type (snake_case)</item>
    ///   <item>Configures publish routing for <c>MessageEnvelope&lt;T&gt;</c> to that exchange</item>
    /// </list>
    ///
    /// Referenced assemblies of each provided assembly are also scanned
    /// transitively to find events defined in shared contract libraries.
    /// </summary>
    public static WolverineRabbitMqRegistrationBuilder ApplyMessagesPublishTopology(
        this WolverineRabbitMqRegistrationBuilder builder,
        IReadOnlyCollection<Assembly> assemblies
    )
    {
        // 1. Snake-case conventional routing — handles consumer topology
        //    (queues + bindings for handler-discovered message types).
        builder.UseSnakeCaseConventions();

        // 2. Scan assemblies for IIntegrationEvent types — declare
        //    publish exchanges for explicit/programmatic sends.
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
            var exchangeName = TopologyHelper.TypeNameToSnakeCase(envelopeType);

            builder.PublishToExchange(envelopeType, exchangeName);
            builder.DeclareExchange(exchangeName, ex => ex.ExchangeType = ExchangeType.Topic);
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
            // Skip assemblies that can't be loaded (e.g. native, different platforms)
            return [];
        }
    }
}
