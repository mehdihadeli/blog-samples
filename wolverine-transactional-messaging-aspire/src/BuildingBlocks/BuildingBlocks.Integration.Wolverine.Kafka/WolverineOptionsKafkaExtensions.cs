using BuildingBlocks.Integration.Wolverine.Configuration;
using Wolverine;
using Wolverine.Kafka;

namespace BuildingBlocks.Integration.Wolverine.Kafka;

public static class WolverineOptionsKafkaExtensions
{
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
