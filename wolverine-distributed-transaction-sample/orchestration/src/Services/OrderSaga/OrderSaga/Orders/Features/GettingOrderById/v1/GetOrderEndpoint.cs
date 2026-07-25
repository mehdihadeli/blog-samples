using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OrderSaga.Orders.Models;
using OrderSaga.Shared.Data;

namespace OrderSaga.Orders.Features.GettingOrderById.v1;

internal static class GetOrderEndpoint
{
    internal static RouteHandlerBuilder MapGetOrderEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/{id:guid}", Handle).WithName("GetOrder");

    private static async Task<IResult> Handle(
        Guid id,
        OrderDbContext dbContext,
        CancellationToken ct)
    {
        var order = await dbContext.Orders.SingleOrDefaultAsync(o => o.Id == id, ct);
        return order is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(OrderResponse.From(order));
    }
}

public sealed record OrderResponse(
    Guid Id,
    string CustomerName,
    decimal Total,
    string Status,
    DateTime CreatedAtUtc)
{
    public static OrderResponse From(Order order) => new(
        order.Id, order.CustomerName, order.Total,
        order.Status.ToString(), order.CreatedAtUtc);
}
