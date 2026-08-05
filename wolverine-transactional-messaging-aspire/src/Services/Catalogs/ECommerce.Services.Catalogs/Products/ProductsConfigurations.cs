using ECommerce.Services.Catalogs.Products.Features.CreatingProduct.v1;
using ECommerce.Services.Catalogs.Products.Features.GettingProductById.v1;
using ECommerce.Services.Catalogs.Products.Features.GettingProductReadModels.v1;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Services.Catalogs.Products;

internal static class ProductsConfigurations
{
    internal static IEndpointRouteBuilder MapProductsModuleEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapCreateProductEndpoint();
        endpoints.MapGetProductByIdEndpoint();
        endpoints.MapGetProductReadModelsEndpoint();

        return endpoints;
    }
}
