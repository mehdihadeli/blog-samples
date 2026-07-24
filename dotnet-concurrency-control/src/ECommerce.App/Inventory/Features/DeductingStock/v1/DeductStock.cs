using ECommerce.Inventory;
using ECommerce.Shared.Contracts;
using MediatR;

namespace ECommerce.Inventory.Features.DeductingStock.v1;

// ═══════════════════════════════════════════════════════════════
//  VERTICAL SLICE: DeductingStock (v1)
//  Core inventory operation — handles all 4 concurrency strategies.
// ═══════════════════════════════════════════════════════════════

internal sealed record DeductStockRequest(
    Guid ProductId,
    int Quantity,
    ConcurrencyStrategy Strategy
) : IRequest<DeductStockResponse>;

internal sealed record DeductStockResponse(
    bool Success,
    int FinalStock,
    string? Error,
    int RetryCount,
    long ElapsedMs,
    ConcurrencyStrategy Strategy
);

internal sealed class DeductStockHandler(
    IProductStore productStore,
    IDistributedLockManager? distributedLockManager = null
) : IRequestHandler<DeductStockRequest, DeductStockResponse>
{
    private readonly object _localLock = new();
    private const int MaxOptimisticRetries = 5;

    public async Task<DeductStockResponse> Handle(
        DeductStockRequest request,
        CancellationToken cancellationToken
    )
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        return request.Strategy switch
        {
            ConcurrencyStrategy.NoLock => HandleNoLock(request, sw),
            ConcurrencyStrategy.LocalLock => HandleLocalLock(request, sw),
            ConcurrencyStrategy.Optimistic => await HandleOptimisticAsync(
                request,
                sw,
                cancellationToken
            ),
            ConcurrencyStrategy.Distributed => await HandleDistributedAsync(
                request,
                sw,
                cancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Strategy)),
        };
    }

    // ── No Lock ───────────────────────────────────────────────
    private DeductStockResponse HandleNoLock(
        DeductStockRequest req,
        System.Diagnostics.Stopwatch sw
    )
    {
        var product = productStore.Get(req.ProductId);
        if (product.Stock < req.Quantity)
            return Fail("Insufficient stock", product.Stock, sw, ConcurrencyStrategy.NoLock);

        Thread.Sleep(Random.Shared.Next(5, 15));
        product.DeductStock(req.Quantity);
        productStore.Write(product);

        return Ok(product.Stock, 0, sw, ConcurrencyStrategy.NoLock);
    }

    // ── Local Lock ────────────────────────────────────────────
    private DeductStockResponse HandleLocalLock(
        DeductStockRequest req,
        System.Diagnostics.Stopwatch sw
    )
    {
        lock (_localLock)
        {
            var product = productStore.Get(req.ProductId);
            if (product.Stock < req.Quantity)
                return Fail("Insufficient stock", product.Stock, sw, ConcurrencyStrategy.LocalLock);

            Thread.Sleep(Random.Shared.Next(5, 15));
            product.DeductStock(req.Quantity);
            productStore.Write(product);

            return Ok(product.Stock, 0, sw, ConcurrencyStrategy.LocalLock);
        }
    }

    // ── Optimistic Concurrency ────────────────────────────────
    private async Task<DeductStockResponse> HandleOptimisticAsync(
        DeductStockRequest req,
        System.Diagnostics.Stopwatch sw,
        CancellationToken ct
    )
    {
        var retryCount = 0;

        while (retryCount <= MaxOptimisticRetries)
        {
            ct.ThrowIfCancellationRequested();
            var product = productStore.Get(req.ProductId);

            if (product.Stock < req.Quantity)
                return Fail(
                    "Insufficient stock",
                    product.Stock,
                    sw,
                    ConcurrencyStrategy.Optimistic,
                    retryCount
                );

            var (success, updated, _) = productStore.TryUpdate(
                req.ProductId,
                product.Version,
                p =>
                {
                    p.DeductStock(req.Quantity);
                    return p;
                }
            );

            if (success)
                return Ok(updated!.Stock, retryCount, sw, ConcurrencyStrategy.Optimistic);

            retryCount++;
            if (retryCount <= MaxOptimisticRetries)
                await Task.Delay(Random.Shared.Next(10, 50) * (int)Math.Pow(2, retryCount), ct);
        }

        return Fail(
            "Max retries exceeded",
            productStore.Get(req.ProductId).Stock,
            sw,
            ConcurrencyStrategy.Optimistic,
            retryCount
        );
    }

    // ── Distributed Lock (3-Layer Approach) ──────────────────
    // Layer 1: Distributed lock — fast coordination for UX (immediate 409 on contention)
    // Layer 2: Optimistic version check inside the lock — safety net for lock expiration
    // Layer 3: Database CHECK constraint (Stock >= 0) — physical guarantee (in schema)
    private async Task<DeductStockResponse> HandleDistributedAsync(
        DeductStockRequest req,
        System.Diagnostics.Stopwatch sw,
        CancellationToken ct
    )
    {
        if (distributedLockManager is null)
            return Fail(
                "No distributed lock manager configured",
                0,
                sw,
                ConcurrencyStrategy.Distributed
            );

        var lease = await distributedLockManager.TryAcquireAsync(
            $"product-lock-{req.ProductId}",
            TimeSpan.FromSeconds(5),
            ct
        );

        if (lease is null)
            return Fail("Distributed lock timeout", 0, sw, ConcurrencyStrategy.Distributed);

        try
        {
            // Layer 2: Optimistic concurrency (safety net inside the lock)
            const int maxRetries = 3;
            var retryCount = 0;

            while (retryCount <= maxRetries)
            {
                ct.ThrowIfCancellationRequested();
                var product = productStore.Get(req.ProductId);

                if (product.Stock < req.Quantity)
                    return Fail(
                        "Insufficient stock",
                        product.Stock,
                        sw,
                        ConcurrencyStrategy.Distributed
                    );

                var (success, updated, _) = productStore.TryUpdate(
                    req.ProductId,
                    product.Version,
                    p =>
                    {
                        p.DeductStock(req.Quantity);
                        return p;
                    }
                );

                if (success)
                    return Ok(updated!.Stock, retryCount, sw, ConcurrencyStrategy.Distributed);

                // Version mismatch — retry within the lock scope
                retryCount++;
                if (retryCount <= maxRetries)
                    await Task.Delay(Random.Shared.Next(10, 30), ct);
            }

            return Fail(
                "Max retries exceeded",
                productStore.Get(req.ProductId).Stock,
                sw,
                ConcurrencyStrategy.Distributed
            );
        }
        finally
        {
            await distributedLockManager.ReleaseAsync(lease);
        }
    }

    // ── Helpers ───────────────────────────────────────────────
    private static DeductStockResponse Ok(
        int stock,
        int retries,
        System.Diagnostics.Stopwatch sw,
        ConcurrencyStrategy s
    ) => new(true, stock, null, retries, sw.ElapsedMilliseconds, s);

    private static DeductStockResponse Fail(
        string error,
        int stock,
        System.Diagnostics.Stopwatch sw,
        ConcurrencyStrategy s,
        int retries = 0
    ) => new(false, stock, error, retries, sw.ElapsedMilliseconds, s);
}
