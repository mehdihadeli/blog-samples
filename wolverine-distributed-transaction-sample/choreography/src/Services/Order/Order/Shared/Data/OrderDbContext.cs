using Microsoft.EntityFrameworkCore;
using Order.Orders.Models;

namespace Order.Shared.Data;

public sealed class OrderDbContext : DbContext
{
    public DbSet<Order.Orders.Models.Order> Orders => Set<Order.Orders.Models.Order>();

    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order.Orders.Models.Order>(o =>
        {
            o.HasKey(x => x.Id);
            o.Property(x => x.CustomerName).HasMaxLength(128);
            o.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            o.Property(x => x.Total).HasColumnType("decimal(18,2)");
        });
    }
}
