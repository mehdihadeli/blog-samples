using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Services.Catalogs.Products.Features.CreatingProduct.v1;

internal static class CreateProductEndpoint
{
    internal static RouteHandlerBuilder MapCreateProductEndpoint(
        this IEndpointRouteBuilder endpoints
    )
    {
        return endpoints.MapPost("/products", Handle).WithName("CreateProduct");
    }

    private static async Task<CreatedAtRoute<CreateProductResponse>> Handle(
        [FromBody] CreateProductRequest request,
        ISender sender,
        CancellationToken cancellationToken
    )
    {
        var command = new CreateProduct(request.Code, request.Name, request.Price);
        var result = await sender.Send(command, cancellationToken);

        return TypedResults.CreatedAtRoute(
            new CreateProductResponse(result.Id, result.Code, result.Name, result.Price),
            "GetProductById",
            new { id = result.Id }
        );
    }
}

internal sealed record CreateProductRequest(string Code, string Name, decimal Price);

internal sealed record CreateProductResponse(Guid Id, string Code, string Name, decimal Price);
