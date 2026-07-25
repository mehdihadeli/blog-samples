using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderSaga.Shared.Data;
using Wolverine.EntityFrameworkCore;

namespace OrderSaga.Shared.Extensions.HostApplicationBuilderExtensions;

public static class StorageExtensions
{
    public static WebApplicationBuilder AddOrderSagaStorage(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("ordersdb")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ordersdb");

        builder.Services.AddDbContextWithWolverineIntegration<OrderDbContext>(opts =>
            opts.UseNpgsql(connectionString));

        return builder;
    }
}
