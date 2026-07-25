using Microsoft.EntityFrameworkCore;
using OrderSaga.Orders.Models;

namespace OrderSaga.Shared.Data;

public sealed class OrderDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(o =>
        {
            o.HasKey(x => x.Id);
            o.Property(x => x.CustomerName).HasMaxLength(128);
            o.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            o.Property(x => x.Total).HasColumnType("decimal(18,2)");
        });
    }
}
