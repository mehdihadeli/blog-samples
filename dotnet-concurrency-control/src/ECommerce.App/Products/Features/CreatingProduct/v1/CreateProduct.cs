using System.Text.Json.Serialization;
using ECommerce.Products.Models;
using ECommerce.Shared.Contracts;
using MediatR;

namespace ECommerce.Products.Features.CreatingProduct.v1;

// ═══════════════════════════════════════════════════════════════
//  VERTICAL SLICE: CreatingProduct (v1)
//  Folder: Products/Features/CreatingProduct/v1/
//  Pattern: {Verb}{Noun}.cs — matches Catalogs conventions.
//  Request/response records co-located in the same file.
// ═══════════════════════════════════════════════════════════════

internal sealed record CreateProductRequest(
    string Name,
    [property: JsonPropertyName("initialStock")] int InitialStock,
    decimal Price
) : IRequest<CreateProductResponse>;

internal sealed record CreateProductResponse(Guid ProductId, string Name, int Stock, decimal Price);

internal sealed class CreateProductHandler(IProductStore productStore)
    : IRequestHandler<CreateProductRequest, CreateProductResponse>
{
    public Task<CreateProductResponse> Handle(
        CreateProductRequest request,
        CancellationToken cancellationToken
    )
    {
        var product = Product.Create(request.Name, request.InitialStock, request.Price);
        productStore.Seed(product);

        return Task.FromResult(
            new CreateProductResponse(product.Id, product.Name, product.Stock, request.Price)
        );
    }
}
