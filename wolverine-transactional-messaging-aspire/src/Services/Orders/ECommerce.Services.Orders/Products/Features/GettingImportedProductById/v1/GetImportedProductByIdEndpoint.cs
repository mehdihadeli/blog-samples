using ECommerce.Services.Orders.Shared.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Orders.Products.Features.GettingImportedProductById.v1;

internal static class GetImportedProductByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetImportedProductByIdEndpoint(
        this IEndpointRouteBuilder endpoints
    )
    {
        return endpoints.MapGet("/products/{id:guid}", Handle).WithName("GetImportedProductById");
    }

    private static async Task<Results<Ok<ImportedProductDetailsResponse>, NotFound>> Handle(
        Guid id,
        OrdersDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        var product = await dbContext.ImportedProducts.SingleOrDefaultAsync(
            x => x.Id == id,
            cancellationToken
        );

        if (product is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(
            new ImportedProductDetailsResponse(
                product.Id,
                product.Code,
                product.Name,
                product.Price,
                product.SourceCreatedAtUtc,
                product.ReceivedAtUtc
            )
        );
    }
}

internal sealed record ImportedProductDetailsResponse(
    Guid Id,
    string Code,
    string Name,
    decimal Price,
    DateTime SourceCreatedAtUtc,
    DateTime ReceivedAtUtc
);
