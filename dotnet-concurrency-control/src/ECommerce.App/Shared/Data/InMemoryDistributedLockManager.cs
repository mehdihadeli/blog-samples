using System.Collections.Concurrent;
using ECommerce.Shared.Contracts;

namespace ECommerce.Shared.Data;

// In-memory distributed lock manager — for demo purposes only.
// In production, replace with Redis (RedLock), Azure Blob Leases, or ZooKeeper.
public sealed class InMemoryDistributedLockManager : IDistributedLockManager
{
    private readonly ConcurrentDictionary<string, LockLease> _locks = new();
    private readonly TimeSpan _minTtl = TimeSpan.FromMilliseconds(100);
    private readonly TimeSpan _lockAcquisitionTimeout = TimeSpan.FromSeconds(3);

    public async Task<LockLease?> TryAcquireAsync(
        string resourceKey,
        TimeSpan ttl,
        CancellationToken ct = default
    )
    {
        // Simulate network latency — distributed lock acquisition
        // always involves at least one round-trip
        await Task.Delay(Random.Shared.Next(1, 5), ct);

        // Clean up expired leases
        foreach (var (key, existing) in _locks)
        {
            if (DateTime.UtcNow >= existing.ExpiresAt)
                _locks.TryRemove(key, out _);
        }

        ttl = ttl < _minTtl ? _minTtl : ttl;
        var ownerId = Guid.NewGuid().ToString("N");
        var lease = new LockLease(resourceKey, ownerId, DateTime.UtcNow.Add(ttl));

        var deadline = DateTime.UtcNow.Add(_lockAcquisitionTimeout);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (_locks.TryAdd(resourceKey, lease))
                return lease;

            await Task.Delay(Random.Shared.Next(5, 20), ct);
        }

        return null;
    }

    public Task ReleaseAsync(LockLease lease)
    {
        if (
            _locks.TryGetValue(lease.ResourceKey, out var current)
            && current.OwnerId == lease.OwnerId
        )
        {
            _locks.TryRemove(lease.ResourceKey, out _);
        }

        return Task.CompletedTask;
    }
}
