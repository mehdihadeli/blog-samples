using ECommerce.Services.Catalogs.Products.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace ECommerce.Services.Catalogs.Shared.Data;

public sealed class CatalogsDbContext(DbContextOptions<CatalogsDbContext> options)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.MapWolverineEnvelopeStorage();

        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("products");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Price).HasPrecision(18, 2);
            builder.Property(x => x.CreatedAtUtc).IsRequired();
        });
    }
}
