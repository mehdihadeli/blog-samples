using ECommerce.Services.Catalogs.Products.Features.ProjectingProductReadModel.v1;
using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.ReadModels;
using ECommerce.Services.Catalogs.TestShared;
using ECommerce.Services.Shared.Contracts.InternalCommands;
using Tests.Shared;

namespace ECommerce.Services.Catalogs.UnitTests;

public class ProjectProductReadModelHandlerTests
{
    [Fact]
    public async Task Handle_ShouldUpsert_ProductReadModel()
    {
        var repository = new FakeProductReadRepository();
        var before = DateTime.UtcNow;

        await ProjectProductReadModelHandler.Handle(
            new ProjectProductReadModel(
                SampleData.ProductId,
                CatalogsTestData.ProductCode,
                CatalogsTestData.ProductName,
                CatalogsTestData.ProductPrice,
                SampleData.CreatedAtUtc
            ),
            repository,
            CancellationToken.None
        );

        var after = DateTime.UtcNow;

        Assert.NotNull(repository.LastUpserted);
        Assert.Equal(SampleData.ProductId, repository.LastUpserted!.Id);
        Assert.Equal(CatalogsTestData.ProductCode, repository.LastUpserted.Code);
        Assert.Equal(CatalogsTestData.ProductName, repository.LastUpserted.Name);
        Assert.Equal(CatalogsTestData.ProductPrice, repository.LastUpserted.Price);
        Assert.Equal(SampleData.CreatedAtUtc, repository.LastUpserted.CreatedAtUtc);
        Assert.InRange(repository.LastUpserted.ProjectedAtUtc, before, after);
    }

    private sealed class FakeProductReadRepository : IProductReadRepository
    {
        public ProductReadModel? LastUpserted { get; private set; }

        public Task<IReadOnlyList<ProductReadModel>> GetAllAsync(
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult<IReadOnlyList<ProductReadModel>>(
                Array.Empty<ProductReadModel>()
            );
        }

        public Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult<ProductReadModel?>(null);
        }

        public Task UpsertAsync(ProductReadModel readModel, CancellationToken cancellationToken)
        {
            LastUpserted = readModel;
            return Task.CompletedTask;
        }
    }
}
