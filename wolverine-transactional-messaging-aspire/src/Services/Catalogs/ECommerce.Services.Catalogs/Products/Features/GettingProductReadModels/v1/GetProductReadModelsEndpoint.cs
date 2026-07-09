using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.ReadModels;
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
        IProductReadRepository repository,
        CancellationToken cancellationToken
    )
    {
        return TypedResults.Ok(await repository.GetAllAsync(cancellationToken));
    }

    private static async Task<Results<Ok<ProductReadModel>, NotFound>> GetById(
        Guid id,
        IProductReadRepository repository,
        CancellationToken cancellationToken
    )
    {
        var readModel = await repository.GetByIdAsync(id, cancellationToken);
        return readModel is null ? TypedResults.NotFound() : TypedResults.Ok(readModel);
    }
}
