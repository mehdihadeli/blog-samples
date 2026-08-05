using ECommerce.Services.Orders.Products.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace ECommerce.Services.Orders.Shared.Data;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<ImportedProduct> ImportedProducts => Set<ImportedProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.MapWolverineEnvelopeStorage();

        modelBuilder.Entity<ImportedProduct>(builder =>
        {
            builder.ToTable("imported_products");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Price).HasPrecision(18, 2);
            builder.Property(x => x.SourceCreatedAtUtc).IsRequired();
            builder.Property(x => x.ReceivedAtUtc).IsRequired();
        });
    }
}
