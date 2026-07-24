using ECommerce.Services.Orders.Products;
using ECommerce.Services.Orders.Shared.Extensions.HostApplicationBuilderExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Services.Orders;

public static class ApplicationConfiguration
{
    public const string OrdersModulePrefixUri = "/api/v1/orders";

    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.AddInfrastructure();
        builder.AddStorage();

        return builder;
    }

    public static IEndpointRouteBuilder MapApplicationEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        var group = endpoints.MapGroup(OrdersModulePrefixUri);
        ((IEndpointRouteBuilder)group).MapProductsModuleEndpoints();

        return endpoints;
    }
}
