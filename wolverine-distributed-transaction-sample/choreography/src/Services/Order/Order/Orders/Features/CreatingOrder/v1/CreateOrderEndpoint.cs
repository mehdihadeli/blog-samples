using Contracts.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Order.Orders.Models;
using Order.Shared.Data;
using Wolverine;

namespace Order.Orders.Features.CreatingOrder.v1;

internal static class CreateOrderEndpoint
{
    internal static RouteHandlerBuilder MapCreateOrderEndpoint(
        this IEndpointRouteBuilder endpoints
    ) => endpoints.MapPost("/", Handle).WithName("CreateOrder");

    private static async Task<IResult> Handle(
        CreateOrderRequest request,
        IMessageBus bus,
        OrderDbContext dbContext,
        CancellationToken ct
    )
    {
        // Create order entity
        var order = Order.Orders.Models.Order.Create(request.CustomerName, request.Total);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(ct);

        // Publish event — Payment service reacts independently
        await bus.PublishAsync(new OrderCreated(order.Id, order.CustomerName, order.Total));

        // Schedule timeout check (30 seconds)
        // If Payment never responds, this handler compensates
        await bus.ScheduleAsync(new OrderTimeoutCheck(order.Id), TimeSpan.FromSeconds(30));

        return TypedResults.Ok(new { order.Id });
    }
}

public sealed record CreateOrderRequest(string CustomerName, decimal Total);
