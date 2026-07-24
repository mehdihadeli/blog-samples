using ECommerce.Services.Orders;
using ECommerce.Services.Orders.Shared.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ECommerce.Services.Orders.Shared.Extensions.WebApplicationExtensions;

public static class WebApplicationExtensions
{
    public static async Task<WebApplication> UseInfrastructureAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        app.MapGet(
            "/",
            () => Results.Ok(new { service = OrdersMetadata.ModuleName, status = "running" })
        );

        return app;
    }
}
