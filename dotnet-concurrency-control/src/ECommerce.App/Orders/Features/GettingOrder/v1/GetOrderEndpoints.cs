using MediatR;

namespace ECommerce.Orders.Features.GettingOrder.v1;

internal static class GetOrderEndpoints
{
    public static RouteGroupBuilder MapGetOrderEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet(
            "/{id:guid}",
            async (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new GetOrderRequest(id));
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
        );
        return group;
    }
}
