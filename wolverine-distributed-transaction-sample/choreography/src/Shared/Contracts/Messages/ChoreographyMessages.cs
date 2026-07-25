using BuildingBlocks.Integration.Wolverine.Messages;

namespace Contracts.Messages;

/// <summary>
/// Published by Order service when an order is created.
/// Payment service listens and reacts.
/// </summary>
public sealed record OrderCreated(Guid OrderId, string CustomerName, decimal Total) : IMessage;

/// <summary>
/// Published by Payment service after successful payment processing.
/// Order service listens and confirms the order.
/// </summary>
public sealed record PaymentProcessed(Guid OrderId, string TransactionId) : IMessage;

/// <summary>
/// Published by Payment service after payment failure.
/// Order service listens and cancels the order (compensation).
/// </summary>
public sealed record PaymentFailed(Guid OrderId, string Reason) : IMessage;

/// <summary>
/// Scheduled message for timeout handling.
/// If order is still Pending when this fires, it gets cancelled.
/// </summary>
public sealed record OrderTimeoutCheck(Guid OrderId) : IMessage;
