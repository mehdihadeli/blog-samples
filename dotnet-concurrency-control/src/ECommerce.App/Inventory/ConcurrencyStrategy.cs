namespace ECommerce.Inventory;

public enum ConcurrencyStrategy
{
    NoLock, // Unsafe — baseline for comparison
    LocalLock, // lock/SemaphoreSlim — single-process only
    Optimistic, // Version check + retry — works across instances
    Distributed // External lock manager — strongest guarantee
    ,
}
