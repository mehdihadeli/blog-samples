using ECommerce.Services.Orders.Shared.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Orders.Products.Features.GettingImportedProducts.v1;

internal static class GetImportedProductsEndpoint
{
    internal static RouteHandlerBuilder MapGetImportedProductsEndpoint(
        this IEndpointRouteBuilder endpoints
    )
    {
        return endpoints.MapGet("/products", Handle).WithName("GetImportedProducts");
    }

    private static async Task<Ok<IReadOnlyList<ImportedProductResponse>>> Handle(
        OrdersDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        var products = await dbContext
            .ImportedProducts.OrderBy(x => x.Name)
            .Select(x => new ImportedProductResponse(
                x.Id,
                x.Code,
                x.Name,
                x.Price,
                x.ReceivedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok<IReadOnlyList<ImportedProductResponse>>(products);
    }
}

internal sealed record ImportedProductResponse(
    Guid Id,
    string Code,
    string Name,
    decimal Price,
    DateTime ReceivedAtUtc
);
