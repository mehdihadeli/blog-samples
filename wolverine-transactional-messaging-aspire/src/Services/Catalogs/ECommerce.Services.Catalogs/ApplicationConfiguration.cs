using ECommerce.Services.Catalogs.Products;
using ECommerce.Services.Catalogs.Shared.Extensions.HostApplicationBuilderExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Services.Catalogs;

public static class ApplicationConfiguration
{
    public const string CatalogModulePrefixUri = "/api/v1/catalogs";

    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.AddStorage();

        return builder;
    }

    public static IEndpointRouteBuilder MapApplicationEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        var group = endpoints.MapGroup(CatalogModulePrefixUri);
        group.MapProductsModuleEndpoints();

        return endpoints;
    }
}
