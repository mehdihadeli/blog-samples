using BuildingBlocks.Integration.Wolverine;
using Wolverine;
using Wolverine.RabbitMQ;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ;

public static class WolverineOptionsRabbitMqExtensions
{
    public static WolverineOptions UseRabbitMqTransport(
        this WolverineOptions options,
        string connectionName,
        Action<WolverineOptions> configure
    )
    {
        options.UseRabbitMqUsingNamedConnection(connectionName).AutoProvision();
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

    public static WolverineOptions ListenToRabbitQueueTransport(
        this WolverineOptions options,
        string queueName
    )
    {
        WolverineMessageTopologyExtensions.RegisterListenerTopology(
            queueName,
            queueName,
            queueName
        );
        options.ListenToRabbitQueue(queueName);

        return options;
    }
}
