using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace BuildingBlocks.Persistence.Mongo.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDatabase(
        this IServiceCollection services,
        string connectionString
    )
    {
        var mongoUrl = MongoUrl.Create(connectionString);
        var databaseName =
            mongoUrl.DatabaseName
            ?? throw new InvalidOperationException(
                "MongoDB connection string must contain a database name."
            );

        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoUrl));
        services.AddSingleton(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName)
        );

        return services;
    }
}
