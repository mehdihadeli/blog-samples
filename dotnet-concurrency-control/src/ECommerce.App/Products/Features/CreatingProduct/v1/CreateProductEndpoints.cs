using MediatR;

namespace ECommerce.Products.Features.CreatingProduct.v1;

internal static class CreateProductEndpoints
{
    public static RouteGroupBuilder MapCreateProductEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/",
            async (CreateProductRequest request, ISender sender) =>
            {
                var result = await sender.Send(request);
                return Results.Created($"/api/products/{result.ProductId}", result);
            }
        );
        return group;
    }
}
