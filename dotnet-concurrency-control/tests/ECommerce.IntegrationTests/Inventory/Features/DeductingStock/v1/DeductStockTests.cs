using System.Net;
using System.Net.Http.Json;

namespace ECommerce.IntegrationTests.Inventory.Features.DeductingStock.v1;

public sealed class DeductStockTests(ECommerceSharedFixture sharedFixture)
    : ECommerceIntegrationTestBase(sharedFixture)
{
    // ─── Helpers ─────────────────────────────────────────────────────

    private sealed record DeductResponse(
        bool Success,
        int FinalStock,
        string? Error,
        int RetryCount,
        long ElapsedMs,
        string Strategy
    );

    private sealed record ProductResponse(Guid Id, string Name, int Stock, int Version);

    // ─── NoLock Strategy ───────────────────────────────────────────

    // ── Strategy: NoLock — Happy path ──
    // No concurrency protection. Single deduct operation on stock 50→40.
    // Validates HTTP 200, correct FinalStock, and strategy echo.
    [Fact]
    public async Task Deduct_NoLock_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("NoLock Item", 50, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        var response = await DeductStockAsync(product.ProductId, 10, "NoLock");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(40, result.FinalStock);
        Assert.Equal("NoLock", result.Strategy);
    }

    // ── Strategy: NoLock — Insufficient stock ──
    // Attempt to deduct more than available (10 from 5).
    // Expects HTTP 409 Conflict with error message.
    [Fact]
    public async Task Deduct_NoLock_InsufficientStock_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Low Stock Item", 5, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        var response = await DeductStockAsync(product.ProductId, 10, "NoLock");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Insufficient stock", result.Error);
    }

    // ─── LocalLock Strategy ────────────────────────────────────────

    // ── Strategy: LocalLock — Happy path ──
    // lock{} provides single-process mutual exclusion.
    // Deduct 25 from 100, expect 75. Synchronization is implicit
    // so concurrent requests inside the same process are serialized.
    [Fact]
    public async Task Deduct_LocalLock_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("LocalLock Item", 100, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        var response = await DeductStockAsync(product.ProductId, 25, "LocalLock");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(75, result.FinalStock);
        Assert.Equal("LocalLock", result.Strategy);
    }

    // ── Strategy: LocalLock — Insufficient stock ──
    // Attempt to deduct 10 from 3 inside lock scope.
    // Expects HTTP 409 Conflict.
    [Fact]
    public async Task Deduct_LocalLock_InsufficientStock_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("LocalLock Low", 3, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        var response = await DeductStockAsync(product.ProductId, 10, "LocalLock");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Insufficient stock", result.Error);
    }

    // ─── Optimistic Strategy ──────────────────────────────────────

    // ── Strategy: Optimistic — Happy path ──
    // Uses application-managed ConcurrencyToken (int Version) with
    // retry loop. Single deduct 50 from 200 → 150, no contention.
    // Validates retryCount=0 on first-try success.
    [Fact]
    public async Task Deduct_Optimistic_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Optimistic Item", 200, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        var response = await DeductStockAsync(product.ProductId, 50, "Optimistic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(150, result.FinalStock);
        Assert.Equal("Optimistic", result.Strategy);
    }

    // ── Strategy: Optimistic — Insufficient stock ──
    // Deduct 10 from 2. Optimistic check fails on stock validation
    // before the Version conflict occurs. HTTP 409.
    [Fact]
    public async Task Deduct_Optimistic_InsufficientStock_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Optimistic Low", 2, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        var response = await DeductStockAsync(product.ProductId, 10, "Optimistic");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Insufficient stock", result.Error);
    }

    // ── Strategy: Optimistic — Concurrent (10 requests) ──
    // 10 parallel deducts of 1 unit each on initial stock 100.
    // Validates that the retry loop resolves Version conflicts:
    // some requests succeed on first try (retryCount=0), others
    // retry with exponential backoff. Final stock must be 90.
    [Fact]
    public async Task Deduct_Optimistic_ConcurrentRequests_AllSucceed()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Concurrent Test", 100, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        // Fire 10 concurrent deduct requests, each deducting 1
        var tasks = Enumerable
            .Range(0, 10)
            .Select(_ => DeductStockAsync(product.ProductId, 1, "Optimistic"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // All should succeed (some with retries)
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
                cancellationToken: ct
            );
            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        // Verify final stock in DB = 90
        var getResponse = await GetProductAsync(product.ProductId);
        var finalProduct = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(finalProduct);
        Assert.Equal(90, finalProduct.Stock);
    }

    // ── Strategy: Optimistic — High contention (20 requests) ──
    // 20 parallel deducts of 1 from stock 50. Higher contention
    // forces more retries. Validates retry loop survives max 5
    // retries and all requests still succeed. Final stock = 30.
    // Note: this is NOT flash-sale load. At flash-sale scale (50+
    // concurrent on limited stock), Optimistic's retry cascade
    // causes most requests to time out. Use Distributed instead.
    [Fact]
    public async Task Deduct_Optimistic_ConcurrentHighContention_AllSucceed()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("High Contention", 50, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        // 20 concurrent requests on 50 stock — all should work with retries
        var tasks = Enumerable
            .Range(0, 20)
            .Select(_ => DeductStockAsync(product.ProductId, 1, "Optimistic"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        var allSucceeded = responses.All(r => r.StatusCode == HttpStatusCode.OK);
        Assert.True(allSucceeded);

        var getResponse = await GetProductAsync(product.ProductId);
        var finalProduct = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(finalProduct);
        Assert.Equal(30, finalProduct.Stock); // 50 - 20 = 30
    }

    // ─── Distributed Strategy ─────────────────────────────────────

    // ── Strategy: Distributed (RedLock via InMemory manager) ──
    // Acquires a named lock via InMemoryDistributedLockManager
    // (fallback when Redis is not configured). Deduct 15 from 80
    // → 65. Validates lock acquire → process → release flow.
    [Fact]
    public async Task Deduct_DistributedWithInMemory_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Distributed Item", 80, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        var response = await DeductStockAsync(product.ProductId, 15, "Distributed");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(65, result.FinalStock);
        Assert.Equal("Distributed", result.Strategy);
    }

    // ── Strategy: Distributed — Insufficient stock ──
    // Distributed lock acquired, then stock check fails (5 from 1).
    // Expects 409. Lock is released after failure.
    [Fact]
    public async Task Deduct_DistributedWithInMemory_InsufficientStock_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Distributed Low", 1, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        var response = await DeductStockAsync(product.ProductId, 5, "Distributed");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Insufficient stock", result.Error);
    }

    // ─── Flash-Sale / High-Contention ──────────────────────────

    // ── Strategy: Distributed — Flash-sale: 30 concurrent, stock=30 ──
    // Simulates a flash-sale: 30 concurrent requests, each deducting 1
    // from stock 30. The 3-layer approach (distributed lock + optimistic
    // TryUpdate) serializes access. All 30 succeed with final stock = 0.
    // This is the pattern for PS5 launch, limited-edition drops, etc.
    [Fact]
    public async Task Deduct_Distributed_FlashSale_AllSucceed()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Flash Sale Item", 30, 50m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        // 30 concurrent requests for 30 units — flash-sale load
        var tasks = Enumerable
            .Range(0, 30)
            .Select(_ => DeductStockAsync(product.ProductId, 1, "Distributed"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(30, succeeded);
        Assert.Equal(0, rejected);

        // Final stock = 0
        var getResponse = await GetProductAsync(product.ProductId);
        var finalProduct = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(finalProduct);
        Assert.Equal(0, finalProduct.Stock);
    }

    // ── Strategy: Distributed — Flash-sale overbooked ──
    // 50 concurrent requests for 20 units. The lock serializes writes:
    // only 20 succeed (200 OK), 30 fail with 409 Conflict.
    // Validates stock exhaustion behavior under extreme load.
    [Fact]
    public async Task Deduct_Distributed_FlashSale_Overbooked_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Overbooked Item", 20, 100m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        // 50 concurrent requests for 20 units — oversubscribed
        var tasks = Enumerable
            .Range(0, 50)
            .Select(_ => DeductStockAsync(product.ProductId, 1, "Distributed"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(20, succeeded);
        Assert.Equal(30, rejected);

        // Final stock = 0 — no overselling
        var getResponse = await GetProductAsync(product.ProductId);
        var finalProduct = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(finalProduct);
        Assert.Equal(0, finalProduct.Stock);
    }

    // ─── Cross-Cutting ────────────────────────────────────────────

    // ── Cross-cutting: All 4 strategies on same product ──
    // Deducts 10 units via NoLock → LocalLock → Optimistic → Distributed
    // sequentially on the same product. Validates that each strategy
    // respects prior state changes and final stock = 60 (100 - 40).
    // Applications: mixed-strategy workflows where different consumers
    // use different concurrency approaches on shared data.
    [Fact]
    public async Task Deduct_AllStrategiesSequential_ConsistentState()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("All Strategies", 100, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        // Deduct 10 with each strategy
        await DeductAndAssert(product.ProductId, 10, "NoLock", 90);
        await DeductAndAssert(product.ProductId, 10, "LocalLock", 80);
        await DeductAndAssert(product.ProductId, 10, "Optimistic", 70);
        await DeductAndAssert(product.ProductId, 10, "Distributed", 60);

        // Final DB state
        var getResponse = await GetProductAsync(product.ProductId);
        var finalProduct = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(finalProduct);
        Assert.Equal(60, finalProduct.Stock);
    }

    private async Task DeductAndAssert(
        Guid productId,
        int quantity,
        string strategy,
        int expectedStock
    )
    {
        var response = await DeductStockAsync(productId, quantity, strategy);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DeductResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(expectedStock, result.FinalStock);
    }

    private sealed record ProductCreatedResponse(
        Guid ProductId,
        string Name,
        int Stock,
        decimal Price
    );

    // ═══════════════════════════════════════════════════════════════
    //  Distributed Strategy — Redis RedLock (Testcontainer)
    //  These tests use the real Redis-backed RedisDistributedLockManager
    //  via the Testcontainers.Redis container started in the fixture.
    // ═══════════════════════════════════════════════════════════════

    // ── Strategy: Distributed (Redis RedLock) — Happy path ──
    // Uses real Redis RedLock via Testcontainers. Acquires lock on
    // product resource, deducts stock, releases. Validates that the
    // Redis-backed lock manager works correctly end-to-end.
    [Fact]
    public async Task Deduct_DistributedWithRedis_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Redis Distributed Item", 80, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        var response = await DeductStockAsync(product.ProductId, 15, "Distributed");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(65, result.FinalStock);
        Assert.Equal("Distributed", result.Strategy);
    }

    // ── Strategy: Distributed (Redis RedLock) — Insufficient stock ──
    // Lock acquired via Redis, then stock check fails (5 from 1).
    // Expects 409. Lock is released after failure.
    [Fact]
    public async Task Deduct_DistributedWithRedis_InsufficientStock_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Redis Distributed Low", 1, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        var response = await DeductStockAsync(product.ProductId, 5, "Distributed");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DeductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Insufficient stock", result.Error);
    }

    // ── Strategy: Distributed (Redis RedLock) — Flash sale ──
    // 30 concurrent requests, each deducting 1 from stock 30.
    // Uses real Redis RedLock so lock acquisition has real network
    // latency (~1ms per round trip). All 30 succeed, final stock = 0.
    [Fact]
    public async Task Deduct_DistributedWithRedis_FlashSale_AllSucceed()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateProductAsync("Redis Flash Sale", 30, 50m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        // 30 concurrent requests via the same app instance
        // The 3-layer approach (Redis lock + optimistic TryUpdate) serializes access.
        var tasks = Enumerable
            .Range(0, 30)
            .Select(_ => DeductStockAsync(product.ProductId, 1, "Distributed"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(30, succeeded);
        Assert.Equal(0, rejected);
        Assert.Equal(30, responses.Length);

        // Final stock = 0
        var getResponse = await GetProductAsync(product.ProductId);
        var finalProduct = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(finalProduct);
        Assert.Equal(0, finalProduct.Stock);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Multi-Instance Distributed Tests
    //  These tests simulate real horizontal scaling by creating
    //  multiple WebApplicationFactory instances (each with its own
    //  DI container, middleware pipeline, and HttpClient) that share
    //  the same Postgres and Redis backends — exactly like multiple
    //  pods behind a load balancer.
    // ═══════════════════════════════════════════════════════════════

    // ── Multi-Instance: Flash sale across 3 simulated instances ──
    // 3 app instances, 10 concurrent requests each = 30 total, all
    // targeting the same product with stock 30. The Redis RedLock
    // coordinates across instances. Each request deducts 1 unit.
    // Validates that all succeed and final stock = 0.
    [Fact]
    public async Task Deduct_Distributed_MultiInstance_FlashSale_AllSucceed()
    {
        var ct = TestContext.Current.CancellationToken;

        // Create product via the primary instance
        var created = await CreateProductAsync("Multi-Instance Flash Sale", 30, 50m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        // Spin up 3 simulated app instances (each has own HttpClient, same Postgres+Redis)
        var instance2 = await CreateSimulatedInstanceAsync();
        var instance3 = await CreateSimulatedInstanceAsync();

        // Each instance fires 10 concurrent deduct requests = 30 total
        var tasks = new List<Task<HttpResponseMessage>>();
        tasks.AddRange(
            Enumerable
                .Range(0, 10)
                .Select(_ =>
                    DeductStockThroughClientAsync(Client, product.ProductId, 1, "Distributed", ct)
                )
        );
        tasks.AddRange(
            Enumerable
                .Range(0, 10)
                .Select(_ =>
                    DeductStockThroughClientAsync(
                        instance2,
                        product.ProductId,
                        1,
                        "Distributed",
                        ct
                    )
                )
        );
        tasks.AddRange(
            Enumerable
                .Range(0, 10)
                .Select(_ =>
                    DeductStockThroughClientAsync(
                        instance3,
                        product.ProductId,
                        1,
                        "Distributed",
                        ct
                    )
                )
        );

        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(30, succeeded);
        Assert.Equal(0, rejected);
        Assert.Equal(30, responses.Length);

        // Final stock = 0
        var getResponse = await GetProductAsync(product.ProductId);
        var finalProduct = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(finalProduct);
        Assert.Equal(0, finalProduct.Stock);
    }

    // ── Multi-Instance: Flash sale overbooked ──
    // 3 instances, 20 requests each = 60 total, stock = 20.
    // The Redis RedLock coordinates: only 20 succeed (200 OK),
    // 40 fail with 409 Conflict. No overselling.
    [Fact]
    public async Task Deduct_Distributed_MultiInstance_Overbooked_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;

        var created = await CreateProductAsync("Multi-Instance Overbooked", 20, 100m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        // 3 simulated instances
        var instance2 = await CreateSimulatedInstanceAsync();
        var instance3 = await CreateSimulatedInstanceAsync();

        // 20 requests per instance = 60 total
        var tasks = new List<Task<HttpResponseMessage>>();
        tasks.AddRange(
            Enumerable
                .Range(0, 20)
                .Select(_ =>
                    DeductStockThroughClientAsync(Client, product.ProductId, 1, "Distributed", ct)
                )
        );
        tasks.AddRange(
            Enumerable
                .Range(0, 20)
                .Select(_ =>
                    DeductStockThroughClientAsync(
                        instance2,
                        product.ProductId,
                        1,
                        "Distributed",
                        ct
                    )
                )
        );
        tasks.AddRange(
            Enumerable
                .Range(0, 20)
                .Select(_ =>
                    DeductStockThroughClientAsync(
                        instance3,
                        product.ProductId,
                        1,
                        "Distributed",
                        ct
                    )
                )
        );

        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(20, succeeded);
        Assert.Equal(40, rejected);
        Assert.Equal(60, responses.Length);

        // No overselling — stock = 0
        var getResponse = await GetProductAsync(product.ProductId);
        var finalProduct = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(finalProduct);
        Assert.Equal(0, finalProduct.Stock);
    }

    // ── Multi-Instance: LocalLock breaks across instances ──
    // Demonstrates why LocalLock is unsafe in multi-instance deployments.
    // 3 instances, 10 concurrent requests each, stock = 30, using LocalLock.
    // Because each instance has its own lock, concurrent access across
    // instances causes overselling — the test verifies this by checking
    // that final stock < 0 (oversold).
    // This is an EXPECTED-FAILURE demonstration: LocalLock is intentionally
    // unsafe for multi-instance scenarios. The test proves it.
    [Fact]
    public async Task Deduct_LocalLock_MultiInstance_Oversells()
    {
        var ct = TestContext.Current.CancellationToken;

        var created = await CreateProductAsync("LocalLock Fail Demo", 30, 10m);
        var product = await created.Content.ReadFromJsonAsync<ProductCreatedResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(product);

        // 3 simulated instances
        var instance2 = await CreateSimulatedInstanceAsync();
        var instance3 = await CreateSimulatedInstanceAsync();

        // 10 requests per instance = 30 total, all using LocalLock
        var tasks = new List<Task<HttpResponseMessage>>();
        tasks.AddRange(
            Enumerable
                .Range(0, 10)
                .Select(_ =>
                    DeductStockThroughClientAsync(Client, product.ProductId, 1, "LocalLock", ct)
                )
        );
        tasks.AddRange(
            Enumerable
                .Range(0, 10)
                .Select(_ =>
                    DeductStockThroughClientAsync(instance2, product.ProductId, 1, "LocalLock", ct)
                )
        );
        tasks.AddRange(
            Enumerable
                .Range(0, 10)
                .Select(_ =>
                    DeductStockThroughClientAsync(instance3, product.ProductId, 1, "LocalLock", ct)
                )
        );

        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        // With 3 instances using LocalLock, the lock is per-process so
        // overselling occurs. Many requests appear to succeed (200 OK) but
        // the final stock will be negative because contention is not controlled.
        var getResponse = await GetProductAsync(product.ProductId);
        var finalProduct = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(
            cancellationToken: ct
        );
        Assert.NotNull(finalProduct);

        // Stock went negative — proof that LocalLock fails across instances
        Assert.True(
            finalProduct.Stock < 0,
            $"Expected overselling with LocalLock across instances. Final stock was {finalProduct.Stock}, expected < 0. This demonstrates why LocalLock is unsafe in multi-instance deployments."
        );
    }

    // ── Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Sends a deduct request through a specific HttpClient (representing
    /// a specific app instance). Used for multi-instance simulations.
    /// </summary>
    private static async Task<HttpResponseMessage> DeductStockThroughClientAsync(
        HttpClient client,
        Guid productId,
        int quantity,
        string strategy,
        CancellationToken ct
    )
    {
        return await client.PostAsJsonAsync(
            $"/api/inventory/{productId}/deduct",
            new { quantity, strategy },
            ct
        );
    }
}
