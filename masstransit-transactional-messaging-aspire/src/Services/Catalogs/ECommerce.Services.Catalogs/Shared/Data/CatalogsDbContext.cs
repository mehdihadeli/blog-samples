using BuildingBlocks.DurableLocalQueue;
using ECommerce.Services.Catalogs.Products.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Catalogs.Shared.Data;

public class CatalogsDbContext(DbContextOptions<CatalogsDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddTransactionalOutboxEntities();
        modelBuilder.AddDurableLocalQueueEntities();
    }
}
