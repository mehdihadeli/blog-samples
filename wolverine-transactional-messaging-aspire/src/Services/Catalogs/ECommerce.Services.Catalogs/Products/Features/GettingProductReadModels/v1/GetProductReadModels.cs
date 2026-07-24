using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.ReadModels;
using MediatR;

namespace ECommerce.Services.Catalogs.Products.Features.GettingProductReadModels.v1;

internal sealed record GetProductReadModels : IRequest<IReadOnlyList<ProductReadModel>>;

internal sealed record GetProductReadModelById(Guid Id) : IRequest<ProductReadModel?>;

internal sealed class GetProductReadModelsHandler(IProductReadRepository repository)
    : IRequestHandler<GetProductReadModels, IReadOnlyList<ProductReadModel>>,
        IRequestHandler<GetProductReadModelById, ProductReadModel?>
{
    public async Task<IReadOnlyList<ProductReadModel>> Handle(
        GetProductReadModels query,
        CancellationToken cancellationToken
    )
    {
        return await repository.GetAllAsync(cancellationToken);
    }

    public async Task<ProductReadModel?> Handle(
        GetProductReadModelById query,
        CancellationToken cancellationToken
    )
    {
        return await repository.GetByIdAsync(query.Id, cancellationToken);
    }
}
