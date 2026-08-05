using BuildingBlocks.Integration.Wolverine.Configuration;
using Wolverine;
using Wolverine.Kafka;

namespace BuildingBlocks.Integration.Wolverine.Kafka.Extensions;

public static class WolverineOptionsExtensions
{
    public static KafkaListenerConfiguration ListenToKafkaTopicTransport(
        this WolverineOptions options,
        string topicName,
        string consumerGroupId,
        WolverineBusOptions? busOptions = null
    )
    {
        var listener = options
            .ListenToKafkaTopic(topicName)
            .ConfigureConsumer(config =>
            {
                config.GroupId = consumerGroupId;
            });

        // Durable inbox: default true, opt-out via UseDurableInboxOnAllListeners = false
        if (busOptions?.UseDurableInboxOnAllListeners ?? true)
        {
            listener.UseDurableInbox();
        }

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
