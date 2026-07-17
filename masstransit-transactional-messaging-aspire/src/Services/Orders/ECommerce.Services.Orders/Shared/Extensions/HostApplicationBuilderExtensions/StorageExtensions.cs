using ECommerce.Services.Orders.Shared.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Services.Orders.Shared.Extensions.HostApplicationBuilderExtensions;

public static class StorageExtensions
{
    public static void AddStorage(this WebApplicationBuilder builder)
    {
        var postgres =
            builder.Configuration.GetConnectionString("ordersdb")
            ?? throw new InvalidOperationException("Missing connection string 'ordersdb'.");

        builder.Services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(postgres));
    }
}
