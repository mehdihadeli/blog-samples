using ECommerce.Services.Orders.Products.Dtos.v1;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

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
        ISender sender,
        CancellationToken cancellationToken
    )
    {
        var dtos = await sender.Send(new GetImportedProducts(), cancellationToken);

        return TypedResults.Ok<IReadOnlyList<ImportedProductResponse>>(
            dtos.Select(ImportedProductResponse.From).ToList()
        );
    }
}

internal sealed record ImportedProductResponse(
    Guid Id,
    string Code,
    string Name,
    decimal Price,
    DateTime ReceivedAtUtc
)
{
    public static ImportedProductResponse From(ImportedProductDto dto)
    {
        return new ImportedProductResponse(
            dto.Id,
            dto.Code,
            dto.Name,
            dto.Price,
            dto.ReceivedAtUtc
        );
    }
}
