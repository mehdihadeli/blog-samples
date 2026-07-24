using ECommerce.Inventory;
using MediatR;

namespace ECommerce.Inventory.Features.DeductingStock.v1;

// Request body for DeductStock (path has productId, body has quantity + strategy)
internal sealed record DeductStockBody(int Quantity, ConcurrencyStrategy Strategy);

internal static class DeductStockEndpoints
{
    public static RouteGroupBuilder MapDeductStockEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/{productId:guid}/deduct",
            async (Guid productId, DeductStockBody body, ISender sender) =>
            {
                var result = await sender.Send(
                    new DeductStockRequest(productId, body.Quantity, body.Strategy)
                );
                return result is { Success: false } ? Results.Conflict(result) : Results.Ok(result);
            }
        );
        return group;
    }
}
