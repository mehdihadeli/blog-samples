using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.RabbitMQ.Extensions;
using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;
using Wolverine.Transports;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ;

public sealed class WolverineRabbitMqRegistrationBuilder
{
    private readonly global::Wolverine.WolverineOptions _options;
    private readonly RabbitMqTransportExpression _transportExpression;
    private readonly WolverineBusOptions? _busOptions;

    internal WolverineRabbitMqRegistrationBuilder(
        global::Wolverine.WolverineOptions options,
        RabbitMqTransportExpression transportExpression,
        WolverineBusOptions? busOptions
    )
    {
        _options = options;
        _transportExpression = transportExpression;
        _busOptions = busOptions;
    }

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

    //  Publish <T> to a named exchange.  The exchange type and any
    //  queue bindings with routing keys are declared separately via
    //  DeclareExchange() / BindQueue() so the topology is fully explicit.
    //
    //  NOTE: we publish via ToRabbitRoutingKey(exchangeName, exchangeName)
    //  (not ToRabbitExchange) because a Topic exchange matches the routing
    //  key against its bindings. ToRabbitExchange sends an empty routing key,
    //  which never matches a binding like "product_created_v1" and the
    //  message is silently dropped. Using the exchange name as the routing
    //  key matches the conventional listener binding
    //  (queue.BindExchange(exchangeName, exchangeName)).

    public WolverineRabbitMqRegistrationBuilder PublishToExchange<T>(string exchangeName)
    {
        _options.PublishMessage<T>().ToRabbitRoutingKey(exchangeName, exchangeName);

        return this;
    }

    /// <summary>
    /// Non-generic overload for runtime reflection-based topology setup.
    /// Publishes messages of the specified <paramref name="messageType"/>
    /// to a RabbitMQ exchange with the given name, using the exchange name
    /// as the routing key (see generic overload for rationale).
    /// </summary>
    public WolverineRabbitMqRegistrationBuilder PublishToExchange(
        Type messageType,
        string exchangeName
    )
    {
        _options.PublishMessage(messageType).ToRabbitRoutingKey(exchangeName, exchangeName);

        return this;
    }

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
