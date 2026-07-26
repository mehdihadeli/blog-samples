using ECommerce;
// ═══════════════════════════════════════════════════════════════
//  E-Commerce · Vertical Slice Architecture
//  Each use case = self-contained slice in Features/{Verb}{Noun}/v1/.
//  Slices share abstractions (IProductStore, IOrderStore), never
//  each other's internals. Concurrency strategies are a parameter
//  to order/inventory slices, not a separate concern.
// ═══════════════════════════════════════════════════════════════

using ECommerce.Shared.Contracts;
using ECommerce.Shared.Data;
using ECommerce.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddApplicationServices();

var app = builder.Build();

// Apply pending EF Core migrations on startup.
// Distributed lock (Redis/RedLock) ensures only one replica runs
// migrations — the other 2 skip. This eliminates the race that
// caused "Failed executing DbCommand" crashes with EnsureCreated.
using (var scope = app.Services.CreateScope())
{
    var lockManager = scope.ServiceProvider.GetService<IDistributedLockManager>();
    var factory = scope.ServiceProvider.GetService<IDbContextFactory<ECommerceDbContext>>();

    if (lockManager is not null && factory is not null)
    {
        var lease = await lockManager.TryAcquireAsync("schema-migration", TimeSpan.FromSeconds(30));
        if (lease is not null)
        {
            try
            {
                using var db = factory.CreateDbContext();
                await db.Database.MigrateAsync();
            }
            finally
            {
                await lockManager.ReleaseAsync(lease);
            }
        }
        // else — another replica holds the lock, skip migration
    }
}

// Swagger UI (reads from built-in /openapi/v1.json)
app.UseSwaggerUI(c => c.SwaggerEndpoint("/openapi/v1.json", "ECommerce API v1"));
app.MapOpenApi();

app.MapECommerceEndpoints();
app.MapDefaultEndpoints();
app.Run();

// Exposed for integration tests via WebApplicationFactory
public partial class Program { }
