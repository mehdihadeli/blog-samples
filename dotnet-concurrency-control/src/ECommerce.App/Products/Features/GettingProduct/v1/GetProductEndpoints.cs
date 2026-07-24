using MediatR;

namespace ECommerce.Products.Features.GettingProduct.v1;

internal static class GetProductEndpoints
{
    public static RouteGroupBuilder MapGetProductEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet(
            "/{id:guid}",
            async (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new GetProductRequest(id));
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
        );
        return group;
    }
}
