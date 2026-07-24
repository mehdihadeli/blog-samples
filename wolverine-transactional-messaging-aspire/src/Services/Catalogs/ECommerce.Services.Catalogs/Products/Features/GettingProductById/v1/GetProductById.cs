using ECommerce.Services.Catalogs.Products.Dtos.v1;
using ECommerce.Services.Catalogs.Shared.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Catalogs.Products.Features.GettingProductById.v1;

internal sealed record GetProductById(Guid Id) : IRequest<ProductDto?>;

internal sealed class GetProductByIdHandler(CatalogsDbContext dbContext)
    : IRequestHandler<GetProductById, ProductDto?>
{
    public async Task<ProductDto?> Handle(GetProductById query, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(
            x => x.Id == query.Id,
            cancellationToken
        );

        return product is null
            ? null
            : new ProductDto(
                product.Id,
                product.Code,
                product.Name,
                product.Price,
                product.CreatedAtUtc
            );
    }
}
