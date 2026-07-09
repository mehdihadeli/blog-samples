using System.Collections.Concurrent;

namespace BuildingBlocks.Integration.Wolverine;

public static class WolverineMessageTopologyExtensions
{
    private static readonly ConcurrentDictionary<
        string,
        RabbitMqPublishTopology
    > PublishTopologiesByMessage = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<
        string,
        RabbitMqListenerTopology
    > ListenerTopologies = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<
        string,
        RabbitMqPublishTopology
    > PublishTopologies = new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterListenerTopology(
        string queueName,
        string exchangeName,
        string routingKey
    )
    {
        ListenerTopologies[queueName] = new RabbitMqListenerTopology(
            queueName,
            exchangeName,
            routingKey
        );
    }

    public static void RegisterPublishTopology<TMessage>(string exchangeName, string routingKey)
    {
        PublishTopologiesByMessage[
            typeof(TMessage).AssemblyQualifiedName
                ?? typeof(TMessage).FullName
                ?? typeof(TMessage).Name
        ] = new RabbitMqPublishTopology(exchangeName, routingKey);
        PublishTopologies[exchangeName] = new RabbitMqPublishTopology(exchangeName, routingKey);
    }

    public static bool TryGetPublishTopology(Type messageType, out RabbitMqPublishTopology topology)
    {
        return PublishTopologiesByMessage.TryGetValue(
            messageType.AssemblyQualifiedName ?? messageType.FullName ?? messageType.Name,
            out topology!
        );
    }

    public static IReadOnlyCollection<RabbitMqListenerTopology> GetRegisteredListenerTopologies()
    {
        return ListenerTopologies.Values.ToArray();
    }

    public static IReadOnlyCollection<RabbitMqPublishTopology> GetRegisteredPublishTopologies()
    {
        return PublishTopologies.Values.ToArray();
    }
}
