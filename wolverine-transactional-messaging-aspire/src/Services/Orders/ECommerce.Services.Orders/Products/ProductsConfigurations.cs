using ECommerce.Services.Orders.Products.Features.GettingImportedProductById.v1;
using ECommerce.Services.Orders.Products.Features.GettingImportedProducts.v1;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Services.Orders.Products;

internal static class ProductsConfigurations
{
    internal static IEndpointRouteBuilder MapProductsModuleEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapGetImportedProductsEndpoint();
        endpoints.MapGetImportedProductByIdEndpoint();

        return endpoints;
    }
}
