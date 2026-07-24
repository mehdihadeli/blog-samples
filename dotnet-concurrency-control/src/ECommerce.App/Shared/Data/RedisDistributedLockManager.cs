using System.Collections.Concurrent;
using ECommerce.Shared.Contracts;
using Medallion.Threading.Redis;
using StackExchange.Redis;

namespace ECommerce.Shared.Data;

/// <summary>
/// Redis-based distributed lock manager using the RedLock algorithm
/// via DistributedLock.Redis (madelson/DistributedLock).
/// Replaces InMemoryDistributedLockManager in production-like setups.
/// </summary>
public sealed class RedisDistributedLockManager : IDistributedLockManager
{
    private readonly ConnectionMultiplexer _mux;
    private readonly IDatabase _database;
    private readonly ConcurrentDictionary<string, RedisDistributedLockHandle> _handles = new();

    public RedisDistributedLockManager(ConnectionMultiplexer multiplexer, int databaseIndex = 0)
    {
        _mux = multiplexer;
        _database = multiplexer.GetDatabase(databaseIndex);
    }

    public async Task<LockLease?> TryAcquireAsync(
        string resourceKey,
        TimeSpan ttl,
        CancellationToken ct = default
    )
    {
        // Fast-fail if Redis isn't reachable.
        if (!_mux.IsConnected)
            return null;

        try
        {
            // Use a short acquire timeout — the lock TTL is separate.
            var acquireTimeout = TimeSpan.FromSeconds(1);
            var redisLock = new RedisDistributedLock(resourceKey, _database);
            var handle = await redisLock.TryAcquireAsync(acquireTimeout, ct);

            if (handle is null)
                return null;

            var ownerId = Guid.NewGuid().ToString("N");
            _handles[ownerId] = handle;

            return new LockLease(resourceKey, ownerId, DateTime.UtcNow.Add(ttl));
        }
        catch (RedisConnectionException)
        {
            // Redis unavailable — return null so caller fails cleanly.
            return null;
        }
    }

    public Task ReleaseAsync(LockLease lease)
    {
        if (_handles.TryRemove(lease.OwnerId, out var handle))
        {
            handle.Dispose();
        }

        return Task.CompletedTask;
    }
}
