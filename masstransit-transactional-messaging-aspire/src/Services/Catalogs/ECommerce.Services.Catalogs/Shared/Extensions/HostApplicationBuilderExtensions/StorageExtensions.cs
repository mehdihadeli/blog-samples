using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.Data;
using ECommerce.Services.Catalogs.Shared.ReadModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace ECommerce.Services.Catalogs.Shared.Extensions.HostApplicationBuilderExtensions;

public static class StorageExtensions
{
    public static void AddStorage(this WebApplicationBuilder builder)
    {
        var postgres =
            builder.Configuration.GetConnectionString("catalogsdb")
            ?? throw new InvalidOperationException("Missing connection string 'catalogsdb'.");
        var mongo =
            builder.Configuration.GetConnectionString("catalogs-mongo")
            ?? "mongodb://localhost:27017";

        builder.Services.AddDbContext<CatalogsDbContext>(options => options.UseNpgsql(postgres));
        builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongo));
        builder.Services.AddScoped(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase("catalogs-mongo")
        );
        builder.Services.AddScoped<IProductReadRepository, MongoProductReadRepository>();
    }
}
