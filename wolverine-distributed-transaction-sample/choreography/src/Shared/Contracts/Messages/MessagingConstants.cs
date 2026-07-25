namespace Contracts.Messages;

/// <summary>
/// RabbitMQ queue name constants for routing between services.
/// </summary>
public static class MessagingConstants
{
    // Choreography: Order service publishes OrderCreated here, Payment listens
    public const string OrderEventsQueue = "order-events";

    // Choreography: Payment publishes responses here, Order listens
    public const string PaymentEventsQueue = "payment-events";
}
