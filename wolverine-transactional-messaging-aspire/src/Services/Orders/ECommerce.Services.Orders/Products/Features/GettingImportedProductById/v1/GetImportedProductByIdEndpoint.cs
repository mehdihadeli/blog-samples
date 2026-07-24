using ECommerce.Services.Orders.Products.Dtos.v1;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

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
        ISender sender,
        CancellationToken cancellationToken
    )
    {
        var productDto = await sender.Send(new GetImportedProductById(id), cancellationToken);

        if (productDto is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ImportedProductDetailsResponse.From(productDto));
    }
}

internal sealed record ImportedProductDetailsResponse(
    Guid Id,
    string Code,
    string Name,
    decimal Price,
    DateTime SourceCreatedAtUtc,
    DateTime ReceivedAtUtc
)
{
    public static ImportedProductDetailsResponse From(ImportedProductDto dto)
    {
        return new ImportedProductDetailsResponse(
            dto.Id,
            dto.Code,
            dto.Name,
            dto.Price,
            dto.SourceCreatedAtUtc,
            dto.ReceivedAtUtc
        );
    }
}
