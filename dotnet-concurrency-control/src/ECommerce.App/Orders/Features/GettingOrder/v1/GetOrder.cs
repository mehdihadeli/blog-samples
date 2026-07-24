using ECommerce.Orders.Models;
using ECommerce.Shared.Contracts;
using MediatR;

namespace ECommerce.Orders.Features.GettingOrder.v1;

// ═══════════════════════════════════════════════════════════════
//  VERTICAL SLICE: GettingOrder (v1)
//  Simple read-only query — response with static From(Order) factory.
// ═══════════════════════════════════════════════════════════════

internal sealed record GetOrderRequest(Guid OrderId) : IRequest<GetOrderResponse?>;

internal sealed record GetOrderResponse(
    Guid Id,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    string? ConcurrencyStrategy,
    DateTime CreatedAtUtc
)
{
    public static GetOrderResponse From(Order order) =>
        new(
            order.Id,
            order.ProductName,
            order.Quantity,
            order.UnitPrice,
            order.TotalPrice,
            order.Status,
            order.ConcurrencyStrategy,
            order.CreatedAtUtc
        );
}

internal sealed class GetOrderHandler(IOrderStore orderStore)
    : IRequestHandler<GetOrderRequest, GetOrderResponse?>
{
    public Task<GetOrderResponse?> Handle(
        GetOrderRequest request,
        CancellationToken cancellationToken
    )
    {
        var order = orderStore.Get(request.OrderId);
        return Task.FromResult(order is null ? null : GetOrderResponse.From(order));
    }
}
