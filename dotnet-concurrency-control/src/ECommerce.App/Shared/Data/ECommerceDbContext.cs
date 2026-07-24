using ECommerce.Orders.Models;
using ECommerce.Products.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Shared.Data;

// EF Core DbContext for PostgreSQL-backed persistence.
// Configured via Fluent API — domain models stay clean.
public sealed class ECommerceDbContext : DbContext
{
    public ECommerceDbContext(DbContextOptions<ECommerceDbContext> options)
        : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Products ──────────────────────────────────────────────
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedNever();

            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Stock).IsRequired();
            entity.Property(p => p.Price).IsRequired().HasColumnType("decimal(18,2)");

            // ── Optimistic concurrency tokens ──────────────────────

            // Application-managed concurrency token.
            // The code must manually increment Version before SaveChanges.
            // Included in UPDATE WHERE clause — on mismatch EF Core
            // throws DbUpdateConcurrencyException.
            entity.Property(p => p.Version).IsConcurrencyToken();

            // Database auto-generated row version.
            // For PostgreSQL (via Npgsql) this maps to the xmin system column,
            // which PostgreSQL increments automatically on every row write.
            // No manual handling needed in code.
            entity.Property(p => p.RowVersion).IsRowVersion();

            entity.Property(p => p.CreatedAtUtc).IsRequired();
            entity.Property(p => p.UpdatedAtUtc).IsRequired();
        });

        // ── Orders ────────────────────────────────────────────────
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).ValueGeneratedNever();

            entity.Property(o => o.ProductId).IsRequired();
            entity.Property(o => o.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(o => o.Quantity).IsRequired();
            entity.Property(o => o.UnitPrice).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(o => o.Status).IsRequired().HasMaxLength(50);
            entity.Property(o => o.ConcurrencyStrategy).HasMaxLength(50);
            entity.Property(o => o.CreatedAtUtc).IsRequired();

            // TotalPrice is computed — not persisted.
            entity.Ignore(o => o.TotalPrice);
        });
    }
}
