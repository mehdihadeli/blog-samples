using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.DurableLocalQueue;

/// <summary>
/// Extension methods for <see cref="ModelBuilder"/> to register the durable local queue entity.
/// Call this in each service's OnModelCreating to add the DurableMessage table.
/// </summary>
public static class DurableLocalQueueModelBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="DurableMessage"/> entity to the model.
    /// The table is created in the same database as the service's domain entities.
    /// </summary>
    public static ModelBuilder AddDurableLocalQueueEntities(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DurableMessage>(entity =>
        {
            entity.ToTable("durable_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TypeName).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Payload).IsRequired().HasColumnType("text");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.EnqueuedAtUtc);
        });

        return modelBuilder;
    }
}
