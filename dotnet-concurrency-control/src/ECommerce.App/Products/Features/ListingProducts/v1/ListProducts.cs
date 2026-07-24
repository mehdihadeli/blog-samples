using ECommerce.Shared.Contracts;
using MediatR;

namespace ECommerce.Products.Features.ListingProducts.v1;

// ═══════════════════════════════════════════════════════════════
//  VERTICAL SLICE: ListingProducts (v1)
//  Another read query — independent slice alongside CreatingProduct.
// ═══════════════════════════════════════════════════════════════

internal sealed record ListProductsRequest : IRequest<ListProductsResponse>;

internal sealed record ListProductsResponse(IReadOnlyList<ProductItem> Products);

internal sealed record ProductItem(Guid Id, string Name, int Stock, int Version, decimal Price);

internal sealed class ListProductsHandler(IProductStore productStore)
    : IRequestHandler<ListProductsRequest, ListProductsResponse>
{
    public Task<ListProductsResponse> Handle(
        ListProductsRequest request,
        CancellationToken cancellationToken
    )
    {
        var products = productStore
            .GetAll()
            .Select(p => new ProductItem(p.Id, p.Name, p.Stock, p.Version, p.Price))
            .ToList();

        return Task.FromResult(new ListProductsResponse(products));
    }
}
