using System.Reflection;
using System.Text.RegularExpressions;
using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.Wolverine.Abstractions;
using BuildingBlocks.Integration.Wolverine.Configuration;
using JasperFx.CodeGeneration.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.RabbitMQ;

namespace BuildingBlocks.Integration.Wolverine;

/// <summary>
/// Single entry point for all Wolverine configuration.
/// Mirrors MassTransit AddTransactionalMassTransit pattern.
/// </summary>
public static class WolverineTransactionalExtensions
{
    public static IHostApplicationBuilder AddTransactionalWolverine(
        this IHostApplicationBuilder builder,
        MessagingTransportType transport,
        Action<TransactionalWolverineConfigurator> configure)
    {
        var configurator = new TransactionalWolverineConfigurator(builder, transport);
        configure(configurator);
        configurator.Apply();
        return builder;
    }
}

public class TransactionalWolverineConfigurator
{
    private readonly IHostApplicationBuilder _builder;
    private readonly MessagingTransportType _transport;
    private readonly List<Action<WolverineOptions>> _wolverineConfigs = [];
    private readonly List<Assembly> _handlerAssemblies = [];
    private Action<WolverineOptions>? _rabbitMqConfig;

    internal TransactionalWolverineConfigurator(
        IHostApplicationBuilder builder,
        MessagingTransportType transport)
    {
        _builder = builder;
        _transport = transport;
    }

    /// <summary>
    /// Add custom Wolverine configuration (e.g., saga queue bindings).
    /// </summary>
    public TransactionalWolverineConfigurator ConfigureWolverine(Action<WolverineOptions> configure)
    {
        _wolverineConfigs.Add(configure);
        return this;
    }

    /// <summary>
    /// Auto-discover integration events from assemblies. Module prefix is derived
    /// from the last segment of each assembly name (lowercased).
    /// Mirrors MassTransit <c>ScanIntegrationEvents(typeof(ApplicationConfiguration).Assembly)</c>.
    /// </summary>
    /// <example>
    /// <c>ScanIntegrationEvents(typeof(CatalogsMetadata).Assembly)</c>
    /// → assembly "Catalog" → prefix "catalog"
    /// → exchange name <c>catalog-product-created</c>.
    /// </example>
    public TransactionalWolverineConfigurator ScanIntegrationEvents(
        params Assembly[] eventAssemblies)
    {
        foreach (var assembly in eventAssemblies)
        {
            var prefix = DeriveModulePrefix(assembly);
            ScanIntegrationEvents(prefix, assembly);
        }
        return this;
    }

    /// <summary>
    /// Scans assemblies for <see cref="IIntegrationEvent"/> types and auto-wires
    /// RabbitMQ publish bindings. Exchange/queue names are derived from the module
    /// prefix and event type name using convention:
    /// <c>{modulePrefix}-{eventName}</c> (kebab-case, version suffix stripped).
    /// </summary>
    /// <example>
    /// <c>ScanIntegrationEvents("catalogs", typeof(ProductCreatedV1).Assembly)</c>
    /// → exchange name <c>catalogs-product-created</c>.
    /// </example>
    public TransactionalWolverineConfigurator ScanIntegrationEvents(
        string modulePrefix,
        params Assembly[] eventAssemblies)
    {
        var scannedEvents = new List<(Type EventType, string Name)>();

        foreach (var assembly in eventAssemblies)
        {
            foreach (var eventType in assembly.GetTypes().Where(t =>
                         t is { IsAbstract: false, IsInterface: false }
                         && typeof(IIntegrationEvent).IsAssignableFrom(t)))
            {
                var name = $"{modulePrefix}-{DeriveEventName(eventType.Name)}";
                scannedEvents.Add((eventType, name));
            }
        }

        if (scannedEvents.Count == 0)
            return this;

        if (_transport == MessagingTransportType.RabbitMq)
        {
            var prev = _rabbitMqConfig;
            _rabbitMqConfig = opts =>
            {
                prev?.Invoke(opts);
                foreach (var (eventType, exchangeName) in scannedEvents)
                {
                    var publishMethod = typeof(WolverineOptions)
                        .GetMethods()
                        .FirstOrDefault(m => m.Name == "PublishMessage" && m.IsGenericMethod)
                        ?.MakeGenericMethod(eventType);

                    if (publishMethod == null) continue;

                    var publishConfig = publishMethod.Invoke(opts, null);
                    if (publishConfig == null) continue;

                    var toRabbitExchangeMethod = publishConfig.GetType()
                        .GetMethod("ToRabbitExchange", [typeof(string)]);
                    toRabbitExchangeMethod?.Invoke(publishConfig, [exchangeName]);
                }
            };
        }

        return this;
    }

    /// <summary>
    /// Auto-wires RabbitMQ listen bindings using assembly name convention
    /// (last segment, lowercased) as module prefix.
    /// Mirrors MassTransit pattern — pass the publisher's marker assembly.
    /// </summary>
    public TransactionalWolverineConfigurator ListenToIntegrationEvents(
        params Assembly[] eventAssemblies)
    {
        foreach (var assembly in eventAssemblies)
        {
            var prefix = DeriveModulePrefix(assembly);
            ListenToIntegrationEvents(prefix, assembly);
        }
        return this;
    }

    /// <summary>
    /// Scans assemblies for <see cref="IIntegrationEvent"/> types and auto-wires
    /// RabbitMQ listen bindings. Queue names are derived using the same convention
    /// as <see cref="ScanIntegrationEvents"/> so publisher and consumer bind to
    /// the same exchange.
    /// </summary>
    public TransactionalWolverineConfigurator ListenToIntegrationEvents(
        string modulePrefix,
        params Assembly[] eventAssemblies)
    {
        var scannedEvents = new List<(Type EventType, string Name)>();

        foreach (var assembly in eventAssemblies)
        {
            foreach (var eventType in assembly.GetTypes().Where(t =>
                         t is { IsAbstract: false, IsInterface: false }
                         && typeof(IIntegrationEvent).IsAssignableFrom(t)))
            {
                var name = $"{modulePrefix}-{DeriveEventName(eventType.Name)}";
                scannedEvents.Add((eventType, name));
            }
        }

        if (scannedEvents.Count == 0)
            return this;

        if (_transport == MessagingTransportType.RabbitMq)
        {
            var prev = _rabbitMqConfig;
            _rabbitMqConfig = opts =>
            {
                prev?.Invoke(opts);
                foreach (var (_, queueName) in scannedEvents)
                {
                    opts.ListenToRabbitQueue(queueName);
                }
            };
        }

        return this;
    }

    /// <summary>
    /// Registers assemblies for Wolverine handler discovery.
    /// Mirrors MassTransit <c>ScanDurableHandlers(typeof(ApplicationConfiguration).Assembly)</c>.
    /// </summary>
    public TransactionalWolverineConfigurator ScanHandlers(
        params Assembly[] handlerAssemblies)
    {
        _handlerAssemblies.AddRange(handlerAssemblies);
        return this;
    }

    /// <summary>
    /// Derive module prefix from assembly name (last segment, lowercased).
    /// Example: "Catalog" → "catalog", "ECommerce.Services.Catalogs" → "catalogs"
    /// </summary>
    private static string DeriveModulePrefix(Assembly assembly)
    {
        var name = assembly.GetName().Name!;
        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0
            ? name[(lastDot + 1)..].ToLowerInvariant()
            : name.ToLowerInvariant();
    }

    /// <summary>
    /// Strip version suffix and convert PascalCase to kebab-case.
    /// Example: "ProductCreatedV1" → "product-created"
    /// </summary>
    private static string DeriveEventName(string typeName)
    {
        var cleaned = Regex.Replace(typeName, @"V\d+$", "");
        return Regex.Replace(cleaned, "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
    }

    internal void Apply()
    {
        switch (_transport)
        {
            case MessagingTransportType.RabbitMq:
                ApplyRabbitMq();
                break;
            case MessagingTransportType.Kafka:
                ApplyKafka();
                break;
            default:
                throw new InvalidOperationException($"Unsupported transport '{_transport}'.");
        }
    }

    private void ApplyRabbitMq()
    {
        _builder.UseWolverine(opts =>
        {
            // Register handler assemblies for Wolverine discovery
            foreach (var assembly in _handlerAssemblies)
                opts.Discovery.IncludeAssembly(assembly);

            // Allow resolving services from DI (required for handlers that use
            // interfaces like IProductReadRepository). Wolverine 6.x defaults
            // to NotAllowed when using dynamic code generation.
            opts.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;

            opts.UseRabbitMqUsingNamedConnection("rabbitmq")
                .AutoProvision();
            opts.Policies.AutoApplyTransactions();

            // Apply custom config (saga queue bindings, etc.)
            foreach (var config in _wolverineConfigs)
                config(opts);

            _rabbitMqConfig?.Invoke(opts);
        });

        RegisterServices();
    }

    private void ApplyKafka()
    {
        _builder.UseWolverine(opts =>
        {
            foreach (var config in _wolverineConfigs)
                config(opts);
        });

        RegisterServices();
    }

    private void RegisterServices()
    {
        _builder.Services.AddScoped<IEventBus, WolverineEventBus>();
        _builder.Services.AddScoped<IInternalCommandBus, WolverineInternalCommandBus>();
    }
}
