namespace BuildingBlocks.Integration.Wolverine;

public sealed record RabbitMqListenerTopology(
    string QueueName,
    string ExchangeName,
    string RoutingKey
);

public sealed record RabbitMqPublishTopology(string ExchangeName, string RoutingKey);
