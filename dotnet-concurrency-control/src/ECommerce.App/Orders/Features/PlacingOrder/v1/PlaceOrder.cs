using ECommerce.Inventory;
using ECommerce.Orders.Models;
using ECommerce.Shared.Contracts;
using MediatR;

namespace ECommerce.Orders.Features.PlacingOrder.v1;

// ═══════════════════════════════════════════════════════════════
//  VERTICAL SLICE: PlacingOrder (v1)
//  Orchestrates product inventory deduction + order creation.
//  Slices share only abstractions (IProductStore) — no direct coupling.
// ═══════════════════════════════════════════════════════════════

internal sealed record PlaceOrderRequest(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    ConcurrencyStrategy Strategy
) : IRequest<PlaceOrderResponse>;

internal sealed record PlaceOrderResponse(
    bool Success,
    Guid? OrderId,
    string? ProductName,
    int Quantity,
    decimal TotalPrice,
    string? Error,
    string Strategy
);

internal sealed class PlaceOrderHandler(
    IProductStore productStore,
    IOrderStore orderStore,
    IDistributedLockManager? distributedLockManager = null
) : IRequestHandler<PlaceOrderRequest, PlaceOrderResponse>
{
    private readonly object _localLock = new();
    private const int MaxOptimisticRetries = 5;

    public async Task<PlaceOrderResponse> Handle(
        PlaceOrderRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!productStore.Exists(request.ProductId))
            return Fail("Product not found", request);

        var product = productStore.Get(request.ProductId);

        var result = request.Strategy switch
        {
            ConcurrencyStrategy.NoLock => PlaceWithNoLock(request, product),
            ConcurrencyStrategy.LocalLock => PlaceWithLocalLock(request),
            ConcurrencyStrategy.Optimistic => await PlaceWithOptimisticAsync(
                request,
                cancellationToken
            ),
            ConcurrencyStrategy.Distributed => await PlaceWithDistributedAsync(
                request,
                cancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(),
        };

        return result;
    }

    // ── No Lock ───────────────────────────────────────────────
    private PlaceOrderResponse PlaceWithNoLock(
        PlaceOrderRequest req,
        Products.Models.Product product
    )
    {
        if (product.Stock < req.Quantity)
            return Fail("Insufficient stock", req);
        Thread.Sleep(Random.Shared.Next(5, 15));
        product.DeductStock(req.Quantity);
        productStore.Write(product);
        return CreateOrder(req, ConcurrencyStrategy.NoLock);
    }

    // ── Local Lock ────────────────────────────────────────────
    private PlaceOrderResponse PlaceWithLocalLock(PlaceOrderRequest req)
    {
        lock (_localLock)
        {
            var product = productStore.Get(req.ProductId);
            if (product.Stock < req.Quantity)
                return Fail("Insufficient stock", req);
            Thread.Sleep(Random.Shared.Next(5, 15));
            product.DeductStock(req.Quantity);
            productStore.Write(product);
            return CreateOrder(req, ConcurrencyStrategy.LocalLock);
        }
    }

    // ── Optimistic ────────────────────────────────────────────
    private async Task<PlaceOrderResponse> PlaceWithOptimisticAsync(
        PlaceOrderRequest req,
        CancellationToken ct
    )
    {
        var retries = 0;
        while (retries <= MaxOptimisticRetries)
        {
            ct.ThrowIfCancellationRequested();
            var product = productStore.Get(req.ProductId);
            if (product.Stock < req.Quantity)
                return Fail("Insufficient stock", req);

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
                return CreateOrder(req, ConcurrencyStrategy.Optimistic);

            retries++;
            if (retries <= MaxOptimisticRetries)
                await Task.Delay(Random.Shared.Next(10, 50) * (int)Math.Pow(2, retries), ct);
        }
        return Fail("Max retries exceeded — order failed", req);
    }

    // ── Distributed ───────────────────────────────────────────
    private async Task<PlaceOrderResponse> PlaceWithDistributedAsync(
        PlaceOrderRequest req,
        CancellationToken ct
    )
    {
        if (distributedLockManager is null)
            return Fail("No distributed lock manager", req);

        var lease = await distributedLockManager.TryAcquireAsync(
            $"product-lock-{req.ProductId}",
            TimeSpan.FromSeconds(5),
            ct
        );

        if (lease is null)
            return Fail("Distributed lock timeout", req);

        try
        {
            var product = productStore.Get(req.ProductId);
            if (product.Stock < req.Quantity)
                return Fail("Insufficient stock", req);
            await Task.Delay(Random.Shared.Next(5, 15), ct);
            product.DeductStock(req.Quantity);
            productStore.Write(product);
            return CreateOrder(req, ConcurrencyStrategy.Distributed);
        }
        finally
        {
            await distributedLockManager.ReleaseAsync(lease);
        }
    }

    // ── Order creation ────────────────────────────────────────
    private PlaceOrderResponse CreateOrder(PlaceOrderRequest req, ConcurrencyStrategy strategy)
    {
        var product = productStore.Get(req.ProductId);
        var order = Order.Create(
            req.ProductId,
            product.Name,
            req.Quantity,
            req.UnitPrice,
            strategy.ToString()
        );
        orderStore.Add(order);

        return new PlaceOrderResponse(
            true,
            order.Id,
            product.Name,
            req.Quantity,
            order.TotalPrice,
            null,
            strategy.ToString()
        );
    }

    private static PlaceOrderResponse Fail(string error, PlaceOrderRequest req) =>
        new(false, null, null, req.Quantity, 0, error, req.Strategy.ToString());
}
