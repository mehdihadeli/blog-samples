using MediatR;

namespace ECommerce.Products.Features.ListingProducts.v1;

internal static class ListProductsEndpoints
{
    public static RouteGroupBuilder MapListProductsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet(
            "/",
            async (ISender sender) =>
            {
                var result = await sender.Send(new ListProductsRequest());
                return Results.Ok(result);
            }
        );
        return group;
    }
}
