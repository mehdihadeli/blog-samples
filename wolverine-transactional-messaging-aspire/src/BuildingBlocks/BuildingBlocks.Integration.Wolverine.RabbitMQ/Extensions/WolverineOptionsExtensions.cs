using BuildingBlocks.Integration.Wolverine.Configuration;
using Wolverine;
using Wolverine.RabbitMQ;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ.Extensions;

public static class WolverineOptionsExtensions
{
    public static global::Wolverine.WolverineOptions PublishToRabbitQueue<T>(
        this global::Wolverine.WolverineOptions options,
        string queueName
    )
    {
        options.PublishMessage<T>().ToRabbitQueue(queueName);

        return options;
    }

    public static RabbitMqListenerConfiguration ListenToRabbitQueueTransport(
        this global::Wolverine.WolverineOptions options,
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
