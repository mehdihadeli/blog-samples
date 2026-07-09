using ECommerce.Services.Catalogs.Products.Models;
using ECommerce.Services.Catalogs.Shared.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

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
        CatalogsDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(
            x => x.Id == id,
            cancellationToken
        );

        if (product is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ProductDetailsResponse.From(product));
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
    public static ProductDetailsResponse From(Product product)
    {
        return new ProductDetailsResponse(
            product.Id,
            product.Code,
            product.Name,
            product.Price,
            product.CreatedAtUtc
        );
    }
}
