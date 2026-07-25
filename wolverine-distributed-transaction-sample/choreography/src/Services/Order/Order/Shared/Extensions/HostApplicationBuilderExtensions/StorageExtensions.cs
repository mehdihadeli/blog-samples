using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Order.Shared.Data;

namespace Microsoft.Extensions.Hosting;

internal static class StorageExtensions
{
    public static void AddOrderStorage(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("ordersdb");
        if (string.IsNullOrWhiteSpace(connectionString))
            return; // Will be handled in AddApplicationServices

        builder.Services.AddDbContext<OrderDbContext>(options =>
            options.UseNpgsql(connectionString)
        );
    }
}
