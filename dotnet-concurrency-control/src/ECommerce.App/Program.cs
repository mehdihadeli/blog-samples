using ECommerce;
// ═══════════════════════════════════════════════════════════════
//  E-Commerce · Vertical Slice Architecture
//  Each use case = self-contained slice in Features/{Verb}{Noun}/v1/.
//  Slices share abstractions (IProductStore, IOrderStore), never
//  each other's internals. Concurrency strategies are a parameter
//  to order/inventory slices, not a separate concern.
// ═══════════════════════════════════════════════════════════════

using ECommerce.Shared.Data;
using ECommerce.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddApplicationServices();

var app = builder.Build();

// Auto-create PostgreSQL tables in development.
// In production, use EF Core migrations instead.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetService<IDbContextFactory<ECommerceDbContext>>();
    if (factory is not null)
    {
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
    }
}

// Swagger UI (reads from built-in /openapi/v1.json)
app.UseSwaggerUI(c => c.SwaggerEndpoint("/openapi/v1.json", "ECommerce API v1"));
app.MapOpenApi();

app.MapECommerceEndpoints();
app.Run();

// Exposed for integration tests via WebApplicationFactory
public partial class Program { }
