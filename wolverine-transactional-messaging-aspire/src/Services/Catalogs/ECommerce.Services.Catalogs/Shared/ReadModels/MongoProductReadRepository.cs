using ECommerce.Services.Catalogs.Shared.Contracts;
using MongoDB.Driver;

namespace ECommerce.Services.Catalogs.Shared.ReadModels;

public sealed class MongoProductReadRepository(IMongoDatabase database) : IProductReadRepository
{
    private readonly IMongoCollection<ProductReadModel> _collection =
        database.GetCollection<ProductReadModel>("catalog_products_read_model");

    public async Task<IReadOnlyList<ProductReadModel>> GetAllAsync(
        CancellationToken cancellationToken
    )
    {
        return await _collection
            .Find(FilterDefinition<ProductReadModel>.Empty)
            .SortBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.Id == id).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(ProductReadModel readModel, CancellationToken cancellationToken)
    {
        await _collection.ReplaceOneAsync(
            x => x.Id == readModel.Id,
            readModel,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );
    }
}
