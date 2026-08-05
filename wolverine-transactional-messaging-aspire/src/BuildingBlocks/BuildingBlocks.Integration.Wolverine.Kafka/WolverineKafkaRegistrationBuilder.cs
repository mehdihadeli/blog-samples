using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Kafka.Extensions;
using Confluent.Kafka.Admin;
using Humanizer;
using Wolverine;
using Wolverine.Kafka;

namespace BuildingBlocks.Integration.Wolverine.Kafka;

public sealed class WolverineKafkaRegistrationBuilder
{
    private readonly WolverineOptions _options;
    private readonly KafkaTransportExpression _transportExpression;
    private readonly WolverineBusOptions? _busOptions;
    private Func<Type, string> _namingConvention = DefaultTopicNaming;

    internal WolverineKafkaRegistrationBuilder(
        WolverineOptions options,
        KafkaTransportExpression transportExpression,
        WolverineBusOptions? busOptions
    )
    {
        _options = options;
        _transportExpression = transportExpression;
        _busOptions = busOptions;
    }

    //  Sets a function that derives topic/consumer-group names from
    //  message types.  Default uses Humanizer.Underscore() on the inner
    //  type (unwrapping MessageEnvelope<T> etc.).
    //
    //  Custom example:
    //    builder.WithNamingConvention(type => "prefix-" + type.Name.Underscore())

    /// <summary>
    /// Override the default naming convention used by topic-free
    /// <c>Publish&lt;T&gt;()</c> and <c>Listen&lt;T&gt;()</c> overloads.
    /// </summary>
    public WolverineKafkaRegistrationBuilder WithNamingConvention(Func<Type, string> convention)
    {
        _namingConvention = convention;
        return this;
    }

    /// <summary>
    /// Use Humanizer snake_case naming for topic/consumer-group names.
    /// <c>ProductCreatedV1</c> → <c>product_created_v1</c>.
    /// This is the default — call explicitly to re-assert after
    /// <c>WithNamingConvention()</c>.
    /// </summary>
    public WolverineKafkaRegistrationBuilder UseSnakeCaseConventions()
    {
        _namingConvention = DefaultTopicNaming;
        return this;
    }

    // ── Publish / Subscribe (implicit naming) ────────────────────────

    /// <summary>Publish &lt;T&gt; to an explicitly named topic.</summary>
    public WolverineKafkaRegistrationBuilder Publish<T>(string topicName)
    {
        _options.PublishMessage<T>().ToKafkaTopic(topicName);

        return this;
    }

    /// <summary>
    /// Publish &lt;T&gt; to a topic named automatically via the
    /// current naming convention (default: snake_case).
    /// </summary>
    public WolverineKafkaRegistrationBuilder Publish<T>()
    {
        var topicName = ResolveTopicName<T>();
        return Publish<T>(topicName);
    }

    /// <summary>
    /// Listen to an explicitly named topic with the given consumer group.
    /// Pass <c>topicName: null</c> to auto-derive via current naming convention.
    /// </summary>
    public WolverineKafkaRegistrationBuilder Listen<T>(
        string? topicName,
        string consumerGroupId,
        Action<KafkaListenerConfiguration>? configure = null
    )
    {
        topicName ??= ResolveTopicName<T>();

        var listener = _options.ListenToKafkaTopicTransport(
            topicName,
            consumerGroupId,
            _busOptions
        );
        listener.DefaultIncomingMessage<T>();
        configure?.Invoke(listener);

        return this;
    }

    /// <summary>
    /// Listen to a topic named automatically via the current naming
    /// convention (default: snake_case). Consumer group also derived
    /// from the message type.
    /// </summary>
    public WolverineKafkaRegistrationBuilder Listen<T>(
        Action<KafkaListenerConfiguration>? configure = null
    )
    {
        var topicName = ResolveTopicName<T>();
        var groupId = ResolveConsumerGroup<T>();
        return Listen<T>(topicName, groupId, configure);
    }

    /// <summary>
    /// Publish &lt;T&gt; to a named topic, optionally with topic creation
    /// specification (NumPartitions, ReplicationFactor).
    /// One-call pattern — no separate DeclareTopic needed.
    /// Mirrors RabbitMQ's <c>PublishToExchange&lt;T&gt;()</c>.
    /// </summary>
    public WolverineKafkaRegistrationBuilder PublishToTopic<T>(
        string topicName,
        Action<TopicSpecification>? configure = null
    )
    {
        var endpoint = _options.PublishMessage<T>().ToKafkaTopic(topicName);

        if (configure != null)
        {
            endpoint.Specification(configure);
        }

        return this;
    }

    //  Wolverine native: routes ALL published message types to Kafka
    //  topics automatically (topic name = type name by default).
    //  No per-type config — discovers at runtime.
    //
    //  See: https://wolverinefx.net/guide/messaging/transports/kafka

    /// <summary>
    /// Auto-route all published message types to Kafka topics by type name.
    /// No per-message-type explicit topology needed.
    /// </summary>
    public WolverineKafkaRegistrationBuilder PublishAllMessages()
    {
        _options.PublishAllMessages().ToKafkaTopics();
        return this;
    }

    //  Matches RabbitMQ's WithTransport() pattern for advanced config
    //  not covered by builder methods (e.g., ConfigureClient,
    //  ConfigureConsumers, ConfigureProducers, topic Specification).

    /// <summary>
    /// Provides direct access to the underlying
    /// <see cref="KafkaTransportExpression"/> for advanced topology
    /// configuration not covered by the builder methods.
    /// </summary>
    public WolverineKafkaRegistrationBuilder WithTransport(
        Action<KafkaTransportExpression> configure
    )
    {
        configure(_transportExpression);
        return this;
    }

    private string ResolveTopicName<T>()
    {
        var inner = UnwrapEnvelope(typeof(T));
        return _namingConvention(inner);
    }

    private string ResolveConsumerGroup<T>()
    {
        var inner = UnwrapEnvelope(typeof(T));
        return _namingConvention(inner);
    }

    private static string DefaultTopicNaming(Type type)
    {
        return type.Name.Underscore();
    }

    private static Type UnwrapEnvelope(Type type)
    {
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
