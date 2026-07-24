using ECommerce.Products.Models;
using ECommerce.Shared.Contracts;
using MediatR;

namespace ECommerce.Products.Features.GettingProduct.v1;

// ═══════════════════════════════════════════════════════════════
//  VERTICAL SLICE: GettingProduct (v1)
//  Read-only query — no side effects.
//  Response record with static From(Product) factory — matches Catalogs.
// ═══════════════════════════════════════════════════════════════

internal sealed record GetProductRequest(Guid ProductId) : IRequest<GetProductResponse?>;

internal sealed record GetProductResponse(
    Guid Id,
    string Name,
    int Stock,
    int Version,
    decimal Price,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
)
{
    public static GetProductResponse From(Product product) =>
        new(
            product.Id,
            product.Name,
            product.Stock,
            product.Version,
            product.Price,
            product.CreatedAtUtc,
            product.UpdatedAtUtc
        );
}

internal sealed class GetProductHandler(IProductStore productStore)
    : IRequestHandler<GetProductRequest, GetProductResponse?>
{
    public Task<GetProductResponse?> Handle(
        GetProductRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!productStore.Exists(request.ProductId))
            return Task.FromResult<GetProductResponse?>(null);

        var product = productStore.Get(request.ProductId);
        return Task.FromResult<GetProductResponse?>(GetProductResponse.From(product));
    }
}
