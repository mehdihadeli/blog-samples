namespace ECommerce.Services.Shared.Contracts.Messaging;

public static class MessagingConstants
{
    public const string ProductCreatedQueue = "catalogs-products-created";
    public const string ProductCreatedTopic = "catalogs-products-created";
    public const string OrdersProductsConsumerGroup = "orders-products";
    public const string DeadLetterQueueName = "wolverine-dead-letter-queue";
}
