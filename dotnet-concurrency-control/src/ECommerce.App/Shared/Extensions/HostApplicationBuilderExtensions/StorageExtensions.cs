using ECommerce.Shared.Contracts;
using ECommerce.Shared.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ECommerce.Shared.Extensions.HostApplicationBuilderExtensions;

// Extension on WebApplicationBuilder — same pattern as Catalogs StorageExtensions.
// Registers all module-level dependencies.
public static class StorageExtensions
{
    public static WebApplicationBuilder AddStorage(this WebApplicationBuilder builder)
    {
        // ── Product & order persistence ─────────────────────────
        // EF Core + PostgreSQL when connection string is configured,
        // otherwise fall back to in-memory stores (dev/demo without Postgres).
        var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrEmpty(postgresConnectionString))
        {
            builder.Services.AddDbContextFactory<ECommerceDbContext>(options =>
                options.UseNpgsql(postgresConnectionString)
            );
            builder.Services.AddSingleton<IProductStore, EfProductStore>();
            builder.Services.AddSingleton<IOrderStore, EfOrderStore>();
        }
        else
        {
            builder.Services.AddSingleton<IProductStore, InventoryStore>();
            builder.Services.AddSingleton<IOrderStore, InMemoryOrderStore>();
        }

        // ── Distributed lock ────────────────────────────────────
        // Redis (RedLock) when connection string present,
        // otherwise fall back to in-memory (dev/demo without Redis).
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            builder.Services.AddSingleton(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString)
            );
            builder.Services.AddSingleton<IDistributedLockManager, RedisDistributedLockManager>();
        }
        else
        {
            builder.Services.AddSingleton<
                IDistributedLockManager,
                InMemoryDistributedLockManager
            >();
        }

        return builder;
    }
}
