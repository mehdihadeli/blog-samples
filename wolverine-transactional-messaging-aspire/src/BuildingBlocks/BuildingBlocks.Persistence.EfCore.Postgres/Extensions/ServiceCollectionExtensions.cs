using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Persistence.EfCore.Postgres.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this IServiceCollection services,
        string connectionString
    )
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(
            options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention(),
            optionsLifetime: ServiceLifetime.Singleton
        );

        return services;
    }
}
