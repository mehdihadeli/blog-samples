using BuildingBlocks.Persistence.EfCore.Postgres.Extensions;
using ECommerce.Services.Orders.Shared.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Services.Orders.Shared.Extensions.HostApplicationBuilderExtensions;

public static class StorageExtensions
{
    public static WebApplicationBuilder AddStorage(this WebApplicationBuilder builder)
    {
        var postgresConnectionString =
            builder.Configuration.GetConnectionString("ordersdb")
            ?? throw new InvalidOperationException("Missing connection string 'ordersdb'.");

        builder.Services.AddPostgresDbContext<OrdersDbContext>(postgresConnectionString);

        return builder;
    }
}
