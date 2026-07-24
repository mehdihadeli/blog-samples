namespace ECommerce.Shared.Contracts;

// Distributed lock manager interface.
// Real-world impls: Redis Redlock, Azure Blob Lease, ZooKeeper ephemeral nodes.
public interface IDistributedLockManager
{
    Task<LockLease?> TryAcquireAsync(
        string resourceKey,
        TimeSpan ttl,
        CancellationToken ct = default
    );

    Task ReleaseAsync(LockLease lease);
}

public sealed record LockLease(string ResourceKey, string OwnerId, DateTime ExpiresAt);
