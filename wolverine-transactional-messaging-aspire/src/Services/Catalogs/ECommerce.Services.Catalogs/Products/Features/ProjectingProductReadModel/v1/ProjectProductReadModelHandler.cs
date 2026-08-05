using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.ReadModels;
using ECommerce.Services.Shared.Contracts.InternalCommands;

namespace ECommerce.Services.Catalogs.Products.Features.ProjectingProductReadModel.v1;

public static class ProjectProductReadModelHandler
{
    public static Task Handle(
        ProjectProductReadModel command,
        IProductReadRepository repository,
        CancellationToken cancellationToken
    )
    {
        return repository.UpsertAsync(
            new ProductReadModel(
                command.ProductId,
                command.Code,
                command.Name,
                command.Price,
                command.CreatedAtUtc,
                DateTime.UtcNow
            ),
            cancellationToken
        );
    }
}
