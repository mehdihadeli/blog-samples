using ECommerce.Services.Catalogs.Shared.Contracts;
using MongoDB.Driver;

namespace ECommerce.Services.Catalogs.Shared.ReadModels;

public sealed class MongoProductReadRepository(IMongoDatabase database) : IProductReadRepository
{
    private readonly IMongoCollection<ProductReadModel> _collection =
        database.GetCollection<ProductReadModel>("product-read-models");

    public Task UpsertAsync(ProductReadModel model, CancellationToken cancellationToken)
    {
        return _collection.ReplaceOneAsync(
            x => x.Id == model.Id,
            model,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );
    }
}
