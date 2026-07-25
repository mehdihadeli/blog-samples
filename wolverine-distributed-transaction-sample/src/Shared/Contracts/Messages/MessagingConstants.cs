namespace Contracts.Messages;

/// <summary>
/// RabbitMQ queue name constants for routing between services.
/// </summary>
public static class MessagingConstants
{
    public const string PaymentRequestsQueue = "payment-requests";
    public const string OrderPaymentResponsesQueue = "order-payment-responses";
}
