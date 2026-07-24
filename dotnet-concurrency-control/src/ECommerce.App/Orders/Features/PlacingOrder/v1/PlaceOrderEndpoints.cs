using MediatR;

namespace ECommerce.Orders.Features.PlacingOrder.v1;

internal static class PlaceOrderEndpoints
{
    public static RouteGroupBuilder MapPlaceOrderEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/",
            async (PlaceOrderRequest request, ISender sender) =>
            {
                var result = await sender.Send(request);
                return result is { Success: false }
                    ? Results.Conflict(result)
                    : Results.Created($"/api/orders/{result.OrderId}", result);
            }
        );
        return group;
    }
}
