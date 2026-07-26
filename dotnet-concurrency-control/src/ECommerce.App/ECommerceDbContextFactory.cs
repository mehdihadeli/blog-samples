using ECommerce.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce;

/// <summary>
/// Design-time factory for EF Core migrations CLI (dotnet ef migrations add).
/// Only used by the tool — never at runtime.
/// </summary>
public sealed class ECommerceDbContextFactory : IDesignTimeDbContextFactory<ECommerceDbContext>
{
    public ECommerceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ECommerceDbContext>();
        var connectionString =
            args.Length > 0
                ? args[0]
                : "Host=localhost;Port=5432;Database=ecommerce;Username=ecommerce;Password=ecommerce";

        optionsBuilder.UseNpgsql(connectionString);
        return new ECommerceDbContext(optionsBuilder.Options);
    }
}
