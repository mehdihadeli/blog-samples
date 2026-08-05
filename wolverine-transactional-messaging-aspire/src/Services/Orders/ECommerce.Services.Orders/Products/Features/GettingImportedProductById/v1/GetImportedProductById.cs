using ECommerce.Services.Orders.Products.Dtos.v1;
using ECommerce.Services.Orders.Shared.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Orders.Products.Features.GettingImportedProductById.v1;

internal sealed record GetImportedProductById(Guid Id) : IRequest<ImportedProductDto?>;

internal sealed class GetImportedProductByIdHandler(OrdersDbContext dbContext)
    : IRequestHandler<GetImportedProductById, ImportedProductDto?>
{
    public async Task<ImportedProductDto?> Handle(
        GetImportedProductById query,
        CancellationToken cancellationToken
    )
    {
        var product = await dbContext.ImportedProducts.SingleOrDefaultAsync(
            x => x.Id == query.Id,
            cancellationToken
        );

        return product is null
            ? null
            : new ImportedProductDto(
                product.Id,
                product.Code,
                product.Name,
                product.Price,
                product.SourceCreatedAtUtc,
                product.ReceivedAtUtc
            );
    }
}
