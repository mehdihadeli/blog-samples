namespace OrderSaga.Orders.Features.CreatingOrder.v1;

/// <summary>
/// Command to start a new order saga (dispatched via IMessageBus.InvokeAsync).
/// Saga handler is in ProcessingOrderPayment feature.
/// </summary>
public sealed record StartOrder(Guid OrderId);
