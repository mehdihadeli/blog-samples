namespace ECommerce.Services.Shared.Contracts.Messaging;

public static class MessagingConstants
{
    public const string ProductCreatedQueue = "catalogs-products-created";
    public const string ProductCreatedTopic = "catalogs-products-created";
    public const string OrdersProductsConsumerGroup = "orders-products";
    public const string OrdersProductsErrorQueue = "orders-products_error";
    public const string OrdersProductsDeadLetterExchange = "orders-products-dlx";
    public const string OrdersProductsDeadLetterQueue = "orders-products-dlq";
    public const string OrdersProductsDeadLetterTopic = "orders-products-dead-letter";
}
