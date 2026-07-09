using ECommerce.Services.Catalogs.Shared.ReadModels;

namespace ECommerce.Services.Catalogs.Shared.Contracts;

public interface IProductReadRepository
{
    Task<IReadOnlyList<ProductReadModel>> GetAllAsync(CancellationToken cancellationToken);

    Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task UpsertAsync(ProductReadModel readModel, CancellationToken cancellationToken);
}
