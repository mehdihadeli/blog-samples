using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Kafka;

namespace BuildingBlocks.Integration.Wolverine.Kafka;

public static class WolverineOptionsKafkaExtensions
{
    public static IHostApplicationBuilder AddWolverineKafka(
        this IHostApplicationBuilder builder,
        WolverineKafkaRegistrationOptions registrationOptions,
        Action<WolverineKafkaRegistrationBuilder>? configure = null
    )
    {
        return builder.AddWolverineMessaging(
            registrationOptions.Common,
            options =>
                options.UseKafkaTransport(
                    registrationOptions.Kafka.ConnectionName,
                    kafkaOptions =>
                    {
                        var registrationBuilder = new WolverineKafkaRegistrationBuilder(
                            kafkaOptions,
                            registrationOptions.Common.Bus
                        );

                        configure?.Invoke(registrationBuilder);
                    },
                    registrationOptions.Common.Bus
                )
        );
    }

    public static WolverineOptions UseKafkaTransport(
        this WolverineOptions options,
        string connectionName,
        Action<WolverineOptions> configure,
        WolverineBusOptions? busOptions = null
    )
    {
        var transport = options.UseKafkaUsingNamedConnection(connectionName).AutoProvision();

        if (!string.IsNullOrWhiteSpace(busOptions?.DeadLetterQueueName))
        {
            transport.DeadLetterQueueTopicName(busOptions.DeadLetterQueueName);
        }

        configure(options);

        return options;
    }

    public static WolverineOptions PublishToKafkaTopic<T>(
        this WolverineOptions options,
        string topicName
    )
    {
        options.PublishMessage<T>().ToKafkaTopic(topicName);

        return options;
    }

    public static KafkaListenerConfiguration ListenToKafkaTopicTransport(
        this WolverineOptions options,
        string topicName,
        string consumerGroupId,
        WolverineBusOptions? busOptions = null
    )
    {
        var listener = options
            .ListenToKafkaTopic(topicName)
            .UseDurableInbox()
            .ConfigureConsumer(config =>
            {
                config.GroupId = consumerGroupId;
            });

        if (busOptions?.UseNativeDeadLetterQueue != false)
        {
            listener.EnableNativeDeadLetterQueue();
        }
        else
        {
            listener.DisableNativeDeadLetterQueue();
        }

        return listener;
    }
}

public sealed class WolverineKafkaRegistrationBuilder
{
    private readonly WolverineOptions _options;
    private readonly WolverineBusOptions? _busOptions;

    internal WolverineKafkaRegistrationBuilder(
        WolverineOptions options,
        WolverineBusOptions? busOptions
    )
    {
        _options = options;
        _busOptions = busOptions;
    }

    public WolverineKafkaRegistrationBuilder Publish<T>(string topicName)
    {
        _options.PublishToKafkaTopic<T>(topicName);

        return this;
    }

    public WolverineKafkaRegistrationBuilder Listen<T>(
        string topicName,
        string consumerGroupId,
        Action<KafkaListenerConfiguration>? configure = null
    )
    {
        var listener = _options.ListenToKafkaTopicTransport(
            topicName,
            consumerGroupId,
            _busOptions
        );
        listener.DefaultIncomingMessage<T>();
        configure?.Invoke(listener);

        return this;
    }
}
