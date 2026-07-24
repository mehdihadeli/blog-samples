using ECommerce.Services.Catalogs.Shared.ReadModels;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Services.Catalogs.Products.Features.GettingProductReadModels.v1;

internal static class GetProductReadModelsEndpoint
{
    internal static RouteHandlerBuilder MapGetProductReadModelsEndpoint(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapGet("/products/read-model", GetAll).WithName("GetProductReadModels");
        return endpoints
            .MapGet("/products/read-model/{id:guid}", GetById)
            .WithName("GetProductReadModelById");
    }

    private static async Task<Ok<IReadOnlyList<ProductReadModel>>> GetAll(
        ISender sender,
        CancellationToken cancellationToken
    )
    {
        return TypedResults.Ok(await sender.Send(new GetProductReadModels(), cancellationToken));
    }

    private static async Task<Results<Ok<ProductReadModel>, NotFound>> GetById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken
    )
    {
        var readModel = await sender.Send(new GetProductReadModelById(id), cancellationToken);
        return readModel is null ? TypedResults.NotFound() : TypedResults.Ok(readModel);
    }
}
