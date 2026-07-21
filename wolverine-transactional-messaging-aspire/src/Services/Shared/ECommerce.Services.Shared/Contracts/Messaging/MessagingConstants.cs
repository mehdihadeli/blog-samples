namespace ECommerce.Services.Shared.Contracts.Messaging;

public static class MessagingConstants
{
    // RabbitMQ: snake_case derived from message type name via
    // TopologyHelper.TypeNameToSnakeCase() — e.g. ProductCreatedV1 → product_created_v1.
    public const string ProductCreatedQueue = "product_created_v1";

    // Kafka: topic name used by Kafka publisher/consumer.
    public const string ProductCreatedTopic = "catalogs-products-created";
    public const string OrdersProductsConsumerGroup = "orders-products";
    public const string DeadLetterQueueName = "wolverine-dead-letter-queue";
}
