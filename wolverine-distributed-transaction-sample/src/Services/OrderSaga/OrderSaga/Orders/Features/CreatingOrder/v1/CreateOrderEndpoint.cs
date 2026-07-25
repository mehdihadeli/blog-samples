using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrderSaga.Orders.Models;
using OrderSaga.Shared.Data;
using Wolverine;

namespace OrderSaga.Orders.Features.CreatingOrder.v1;

internal static class CreateOrderEndpoint
{
    internal static RouteHandlerBuilder MapCreateOrderEndpoint(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/", Handle).WithName("CreateOrder");

    private static async Task<IResult> Handle(
        CreateOrderRequest request,
        IMessageBus bus,
        OrderDbContext dbContext,
        CancellationToken ct)
    {
        var order = Order.Create(request.CustomerName, request.Total);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(ct);

        await bus.InvokeAsync(new StartOrder(order.Id), ct);
        return TypedResults.Ok(new { order.Id });
    }
}

public sealed record CreateOrderRequest(string CustomerName, decimal Total);
