using BuildingBlocks.Abstractions.Messages;

namespace Contracts.Messages;

/// <summary>
/// Sent by OrderSaga to Payment service to process payment.
/// </summary>
public sealed record ProcessPayment(Guid OrderId, decimal Amount) : IMessage;

/// <summary>
/// Published by Payment service after successful processing.
/// </summary>
public sealed record PaymentProcessed(Guid OrderId, string TransactionId) : IMessage;

/// <summary>
/// Published by Payment service after payment failure.
/// </summary>
public sealed record PaymentFailed(Guid OrderId, string Reason) : IMessage;
