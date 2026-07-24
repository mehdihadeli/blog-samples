using ECommerce.Services.Orders.Products.Dtos.v1;
using ECommerce.Services.Orders.Shared.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Orders.Products.Features.GettingImportedProducts.v1;

internal sealed record GetImportedProducts : IRequest<IReadOnlyList<ImportedProductDto>>;

internal sealed class GetImportedProductsHandler(OrdersDbContext dbContext)
    : IRequestHandler<GetImportedProducts, IReadOnlyList<ImportedProductDto>>
{
    public async Task<IReadOnlyList<ImportedProductDto>> Handle(
        GetImportedProducts query,
        CancellationToken cancellationToken
    )
    {
        return await dbContext
            .ImportedProducts.OrderBy(x => x.Name)
            .Select(x => new ImportedProductDto(
                x.Id,
                x.Code,
                x.Name,
                x.Price,
                x.SourceCreatedAtUtc,
                x.ReceivedAtUtc
            ))
            .ToListAsync(cancellationToken);
    }
}
