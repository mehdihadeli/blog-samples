using ECommerce.Services.Catalogs.Shared.ReadModels;

namespace ECommerce.Services.Catalogs.Shared.Contracts;

public interface IProductReadRepository
{
    Task UpsertAsync(ProductReadModel model, CancellationToken cancellationToken);
}
