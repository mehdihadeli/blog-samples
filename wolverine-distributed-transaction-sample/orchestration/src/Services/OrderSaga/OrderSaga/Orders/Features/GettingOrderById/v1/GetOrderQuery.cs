namespace OrderSaga.Orders.Features.GettingOrderById.v1;

/// <summary>
/// Query to retrieve an order by ID.
/// </summary>
public sealed record GetOrderByIdQuery(Guid Id);
