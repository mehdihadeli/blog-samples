using BuildingBlocks.Persistence.EfCore.Postgres.Extensions;
using BuildingBlocks.Persistence.Mongo.Extensions;
using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.Data;
using ECommerce.Services.Catalogs.Shared.ReadModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Services.Catalogs.Shared.Extensions.HostApplicationBuilderExtensions;

public static class StorageExtensions
{
    public static WebApplicationBuilder AddStorage(this WebApplicationBuilder builder)
    {
        var postgresConnectionString =
            builder.Configuration.GetConnectionString("catalogsdb")
            ?? throw new InvalidOperationException("Missing connection string 'catalogsdb'.");
        var mongoConnectionString =
            builder.Configuration.GetConnectionString("catalogs-mongo")
            ?? throw new InvalidOperationException("Missing connection string 'catalogs-mongo'.");

        builder.Services.AddPostgresDbContext<CatalogsDbContext>(postgresConnectionString);
        builder.Services.AddMongoDatabase(mongoConnectionString);
        builder.Services.AddScoped<IProductReadRepository, MongoProductReadRepository>();

        return builder;
    }
}
