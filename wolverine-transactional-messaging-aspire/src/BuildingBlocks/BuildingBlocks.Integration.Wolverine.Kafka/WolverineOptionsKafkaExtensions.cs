using Wolverine;
using Wolverine.Kafka;

namespace BuildingBlocks.Integration.Wolverine.Kafka;

public static class WolverineOptionsKafkaExtensions
{
    public static WolverineOptions UseKafkaTransport(
        this WolverineOptions options,
        string connectionName,
        Action<WolverineOptions> configure
    )
    {
        options.UseKafkaUsingNamedConnection(connectionName).AutoProvision();
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

    public static WolverineOptions ListenToKafkaTopicTransport(
        this WolverineOptions options,
        string topicName,
        string consumerGroupId
    )
    {
        options
            .ListenToKafkaTopic(topicName)
            .UseDurableInbox()
            .ConfigureConsumer(config =>
            {
                config.GroupId = consumerGroupId;
            });

        return options;
    }
}
