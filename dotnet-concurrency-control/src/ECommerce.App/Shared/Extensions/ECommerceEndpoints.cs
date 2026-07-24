using ECommerce.Inventory.Features.DeductingStock.v1;
using ECommerce.Orders.Features.GettingOrder.v1;
using ECommerce.Orders.Features.PlacingOrder.v1;
using ECommerce.Products.Features.CreatingProduct.v1;
using ECommerce.Products.Features.GettingProduct.v1;
using ECommerce.Products.Features.ListingProducts.v1;

namespace ECommerce.Shared.Extensions;

/// <summary>
/// Thin orchestrator — each slice registers its own endpoints.
/// New slices: add a using + one line here.
/// </summary>
public static class ECommerceEndpoints
{
    public static WebApplication MapECommerceEndpoints(this WebApplication app)
    {
        app.MapGroup("/api/products")
            .WithTags("Products")
            .MapCreateProductEndpoints()
            .MapListProductsEndpoints()
            .MapGetProductEndpoints();

        app.MapGroup("/api/inventory").WithTags("Inventory").MapDeductStockEndpoints();

        app.MapGroup("/api/orders")
            .WithTags("Orders")
            .MapPlaceOrderEndpoints()
            .MapGetOrderEndpoints();

        return app;
    }
}
