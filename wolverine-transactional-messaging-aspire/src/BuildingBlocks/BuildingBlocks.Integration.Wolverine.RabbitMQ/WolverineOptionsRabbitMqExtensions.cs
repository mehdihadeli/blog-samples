using System.Reflection;
using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;
using Wolverine.Transports;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ;

public static class WolverineOptionsRabbitMqExtensions
{
    public static IHostApplicationBuilder AddWolverineRabbitMq(
        this IHostApplicationBuilder builder,
        WolverineRabbitMqRegistrationOptions registrationOptions,
        Action<WolverineRabbitMqRegistrationBuilder>? configure = null
    )
    {
        builder.AddWolverineMessaging(
            registrationOptions.Common,
            options =>
            {
                var transport = options.UseRabbitMqUsingNamedConnection(
                    registrationOptions.RabbitMq.ConnectionName
                );

                transport.AutoProvision();

                var busOptions = registrationOptions.Common.Bus;
                if (!string.IsNullOrWhiteSpace(busOptions?.DeadLetterQueueName))
                {
                    transport.CustomizeDeadLetterQueueing(
                        new DeadLetterQueue(busOptions.DeadLetterQueueName)
                    );
                }

                var registrationBuilder = new WolverineRabbitMqRegistrationBuilder(
                    options,
                    transport,
                    busOptions
                );

                configure?.Invoke(registrationBuilder);
            }
        );

        return builder;
    }

    public static WolverineOptions UseRabbitMqTransport(
        this WolverineOptions options,
        string connectionName,
        Action<WolverineOptions> configure,
        WolverineBusOptions? busOptions = null
    )
    {
        var transport = options.UseRabbitMqUsingNamedConnection(connectionName);

        transport.AutoProvision();

        if (!string.IsNullOrWhiteSpace(busOptions?.DeadLetterQueueName))
        {
            transport.CustomizeDeadLetterQueueing(
                new DeadLetterQueue(busOptions.DeadLetterQueueName)
            );
        }

        configure(options);

        return options;
    }

    public static WolverineOptions PublishToRabbitQueue<T>(
        this WolverineOptions options,
        string queueName
    )
    {
        options.PublishMessage<T>().ToRabbitQueue(queueName);

        return options;
    }

    public static RabbitMqListenerConfiguration ListenToRabbitQueueTransport(
        this WolverineOptions options,
        string queueName,
        WolverineBusOptions? busOptions = null
    )
    {
        var listener = options.ListenToRabbitQueue(queueName);

        if (busOptions?.UseNativeDeadLetterQueue == false)
        {
            listener.DisableDeadLetterQueueing();
        }
        else if (!string.IsNullOrWhiteSpace(busOptions?.DeadLetterQueueName))
        {
            listener.DeadLetterQueueing(new DeadLetterQueue(busOptions.DeadLetterQueueName));
        }

        return listener;
    }
}

public sealed class WolverineRabbitMqRegistrationBuilder
{
    private readonly WolverineOptions _options;
    private readonly RabbitMqTransportExpression _transportExpression;
    private readonly WolverineBusOptions? _busOptions;

    internal WolverineRabbitMqRegistrationBuilder(
        WolverineOptions options,
        RabbitMqTransportExpression transportExpression,
        WolverineBusOptions? busOptions
    )
    {
        _options = options;
        _transportExpression = transportExpression;
        _busOptions = busOptions;
    }

    // ── Existing Methods ──────────────────────────────────────────────

    public WolverineRabbitMqRegistrationBuilder Publish<T>(string queueName)
    {
        _options.PublishToRabbitQueue<T>(queueName);

        return this;
    }

    public WolverineRabbitMqRegistrationBuilder Listen<T>(
        string queueName,
        Action<RabbitMqListenerConfiguration>? configure = null
    )
    {
        var listener = _options.ListenToRabbitQueueTransport(queueName, _busOptions);
        listener.DefaultIncomingMessage<T>();
        configure?.Invoke(listener);

        return this;
    }

    // ── 1. Conventional Routing ──────────────────────────────────────
    //
    //  Auto-creates fanout exchange per message type + queue per
    //  message/handler type + binding.  Customizable via the optional
    //  configuration callback (exchange naming, queue naming, listener
    //  tuning, sending tuning, handler-type naming, etc.).
    //
    //  Docs: https://wolverinefx.net/guide/messaging/transports/rabbitmq/conventional-routing.html

    public WolverineRabbitMqRegistrationBuilder UseConventionalRouting(
        Action<RabbitMqMessageRoutingConvention>? configure = null
    )
    {
        if (configure != null)
        {
            _transportExpression.UseConventionalRouting(configure);
        }
        else
        {
            _transportExpression.UseConventionalRouting();
        }

        return this;
    }

    /// <summary>
    /// Use conventional routing with the specified naming source.
    /// <c>NamingSource.FromHandlerType</c> names queues after the handler type
    /// instead of the message type — useful in modular monoliths where multiple
    /// handlers consume the same message type.
    /// </summary>
    /// <param name="namingSource">Source for queue naming (message type or handler type).</param>
    /// <param name="configure">Optional further customization of the convention.</param>
    public WolverineRabbitMqRegistrationBuilder UseConventionalRouting(
        NamingSource namingSource,
        Action<RabbitMqMessageRoutingConvention>? configure = null
    )
    {
        _transportExpression.UseConventionalRouting(namingSource, configure);

        return this;
    }

    // ── 1a. Snake-Case Conventional Routing ──────────────────────────
    //
    //  Uses conventional routing under the hood, but names exchanges
    //  and queues in snake_case (e.g. ProductCreatedV1 → product_created_v1).
    //  Envelope types are automatically unwrapped so the inner message type
    //  drives the naming.
    //
    //  Topology produced per handler:
    //    Exchange:  product_created_v1 (fanout)
    //    Queue:     product_created_v1
    //    Binding:   product_created_v1 → product_created_v1

    /// <summary>
    /// Activates conventional routing with snake_case naming for all
    /// discovered message handlers. Messages wrapped in
    /// <c>MessageEnvelope&lt;T&gt;</c> get their inner type extracted for
    /// naming.
    /// </summary>
    /// <param name="configure">
    /// Optional callback to further tune the convention after snake_case
    /// defaults are set (e.g. add per-type <c>ConfigureListeners</c> bindings).
    /// </param>
    public WolverineRabbitMqRegistrationBuilder UseSnakeCaseConventions(
        Action<RabbitMqMessageRoutingConvention>? configure = null
    )
    {
        _transportExpression.UseConventionalRouting(conventions =>
        {
            conventions.UseSnakeCaseNaming();

            configure?.Invoke(conventions);
        });

        return this;
    }

    // ── 4. Publish to Exchange (routing key via bindings) ────────────
    //
    //  Publish <T> to a named exchange.  The exchange type and any
    //  queue bindings with routing keys are declared separately via
    //  DeclareExchange() / BindQueue() so the topology is fully explicit.

    public WolverineRabbitMqRegistrationBuilder PublishToExchange<T>(string exchangeName)
    {
        _options.PublishMessage<T>().ToRabbitExchange(exchangeName);

        return this;
    }

    // ── 6. Declarative Topology ──────────────────────────────────────
    //
    //  Declare exchanges, queues, and bindings so Wolverine can
    //  auto-provision them at startup (when AutoProvision() is on).

    public WolverineRabbitMqRegistrationBuilder DeclareExchange(
        string exchangeName,
        Action<IRabbitMqBindableExchange>? configure = null
    )
    {
        _transportExpression.DeclareExchange(exchangeName, configure);

        return this;
    }

    public WolverineRabbitMqRegistrationBuilder DeclareQueue(
        string queueName,
        Action<IRabbitMqQueue>? configure = null
    )
    {
        _transportExpression.DeclareQueue(queueName, configure);

        return this;
    }

    /// <summary>Bind a queue to an exchange with an optional routing key.</summary>
    public WolverineRabbitMqRegistrationBuilder BindQueue(
        string queueName,
        string exchangeName,
        string? routingKey = null
    )
    {
        if (routingKey != null)
        {
            _transportExpression.BindExchange(exchangeName).ToQueue(queueName, routingKey);
        }
        else
        {
            _transportExpression.BindExchange(exchangeName).ToQueue(queueName);
        }

        return this;
    }

    /// <summary>Bind a source exchange to a destination exchange with an optional routing key (exchange-to-exchange binding).</summary>
    public WolverineRabbitMqRegistrationBuilder BindExchangeToExchange(
        string sourceExchangeName,
        string destinationExchangeName,
        string? routingKey = null
    )
    {
        if (routingKey != null)
        {
            _transportExpression
                .BindExchange(sourceExchangeName)
                .ToExchange(destinationExchangeName, routingKey);
        }
        else
        {
            _transportExpression
                .BindExchange(sourceExchangeName)
                .ToExchange(destinationExchangeName);
        }

        return this;
    }

    // ── 7. Raw Transport Access ───────────────────────────────────────
    //
    //  Escape hatch for advanced scenarios that need the underlying
    //  RabbitMqTransportExpression (e.g., custom IMessageRoutingConvention,
    //  per-message-type topology config, or runtime reflection).

    /// <summary>
    /// Provides direct access to the underlying <see cref="RabbitMqTransportExpression"/>
    /// for advanced topology configuration not covered by the builder methods.
    /// </summary>
    public WolverineRabbitMqRegistrationBuilder WithTransport(
        Action<RabbitMqTransportExpression> configure
    )
    {
        configure(_transportExpression);
        return this;
    }
}

// ── Snake-case extension for RabbitMqMessageRoutingConvention ────────

/// <summary>
/// Snake-case naming extension for
/// <see cref="RabbitMqMessageRoutingConvention"/>.
/// </summary>
public static class RabbitMqConventionSnakeCaseExtensions
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
        convention.ExchangeNameForSending(type => TopologyHelper.TypeNameToSnakeCase(type));

        // Queue name: product_created_v1
        convention.QueueNameForListener(type => TopologyHelper.TypeNameToSnakeCase(type));

        // Broker identifiers (used internally by Wolverine)
        convention.IdentifierForSender(type => TopologyHelper.TypeNameToSnakeCase(type));

        convention.IdentifierForListener(type => TopologyHelper.TypeNameToSnakeCase(type));

        return convention;
    }
}
