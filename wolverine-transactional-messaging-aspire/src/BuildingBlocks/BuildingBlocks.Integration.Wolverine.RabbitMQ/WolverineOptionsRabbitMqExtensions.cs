using BuildingBlocks.Integration.Wolverine;
using BuildingBlocks.Integration.Wolverine.Configuration;
using Wolverine;
using Wolverine.RabbitMQ;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ;

public static class WolverineOptionsRabbitMqExtensions
{
    public static WolverineOptions UseRabbitMqTransport(
        this WolverineOptions options,
        string connectionName,
        Action<WolverineOptions> configure,
        WolverineBusOptions? busOptions = null
    )
    {
        var transport = options.UseRabbitMqUsingNamedConnection(connectionName).AutoProvision();

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
        WolverineMessageTopologyExtensions.RegisterPublishTopology<T>(queueName, queueName);
        options.PublishMessage<T>().ToRabbitQueue(queueName);

        return options;
    }

    public static RabbitMqListenerConfiguration ListenToRabbitQueueTransport(
        this WolverineOptions options,
        string queueName,
        WolverineBusOptions? busOptions = null
    )
    {
        WolverineMessageTopologyExtensions.RegisterListenerTopology(
            queueName,
            queueName,
            queueName
        );

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
