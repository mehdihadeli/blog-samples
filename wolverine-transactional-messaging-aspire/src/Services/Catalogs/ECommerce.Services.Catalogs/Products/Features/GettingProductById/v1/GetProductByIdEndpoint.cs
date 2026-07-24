using ECommerce.Services.Catalogs.Products.Dtos.v1;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Services.Catalogs.Products.Features.GettingProductById.v1;

internal static class GetProductByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetProductByIdEndpoint(
        this IEndpointRouteBuilder endpoints
    )
    {
        return endpoints.MapGet("/products/{id:guid}", Handle).WithName("GetProductById");
    }

    private static async Task<Results<Ok<ProductDetailsResponse>, NotFound>> Handle(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken
    )
    {
        var productDto = await sender.Send(new GetProductById(id), cancellationToken);

        if (productDto is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ProductDetailsResponse.From(productDto));
    }
}

internal sealed record ProductDetailsResponse(
    Guid Id,
    string Code,
    string Name,
    decimal Price,
    DateTime CreatedAtUtc
)
{
    public static ProductDetailsResponse From(ProductDto productDto)
    {
        return new ProductDetailsResponse(
            productDto.Id,
            productDto.Code,
            productDto.Name,
            productDto.Price,
            productDto.CreatedAtUtc
        );
    }
}
