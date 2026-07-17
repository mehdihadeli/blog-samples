using ECommerce.Services.Orders.Products.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Orders.Shared.Data;

public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<ImportedProduct> ImportedProducts => Set<ImportedProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
