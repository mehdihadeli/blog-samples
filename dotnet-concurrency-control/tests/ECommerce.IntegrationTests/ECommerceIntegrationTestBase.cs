using System.Net.Http.Json;
using ECommerce.Shared.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public abstract class ECommerceIntegrationTestBase : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    // Multi-instance simulation: holds extra factories for distributed tests.
    // Each factory is a fully isolated app instance with its own DI container
    // and HttpClient, sharing only the Postgres and Redis containers.
    private readonly List<(
        WebApplicationFactory<Program> Factory,
        HttpClient Client
    )> _simulatedInstances = [];

    protected ECommerceIntegrationTestBase(ECommerceSharedFixture sharedFixture)
    {
        SharedFixture = sharedFixture;
    }

    protected ECommerceSharedFixture SharedFixture { get; }

    protected WebApplicationFactory<Program> Factory =>
        _factory
        ?? throw new InvalidOperationException(
            "Factory not initialised. Call InitializeAsync first."
        );

    protected HttpClient Client =>
        _client
        ?? throw new InvalidOperationException(
            "Client not initialised. Call InitializeAsync first."
        );

    public virtual async ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:Postgres",
                SharedFixture.Postgres.ConnectionString
            );
            // Wire up Redis Testcontainer for distributed locking tests
            builder.UseSetting("ConnectionStrings:Redis", SharedFixture.Redis.ConnectionString);
        });

        // Ensure DB schema exists
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<ECommerceDbContext>
        >();
        using var db = dbFactory.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        _client = _factory.CreateClient();
    }

    public virtual async ValueTask DisposeAsync()
    {
        foreach (var (factory, client) in _simulatedInstances)
        {
            client.Dispose();
            await factory.DisposeAsync();
        }
        _simulatedInstances.Clear();

        if (_client is not null)
        {
            _client.Dispose();
            _client = null;
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }

        await SharedFixture.ResetAsync();
    }

    /// <summary>
    /// Creates additional simulated app instances for multi-instance concurrency tests.
    /// Each instance gets its own WebApplicationFactory and HttpClient but shares the
    /// same Postgres and Redis backends — exactly like real horizontal scaling.
    /// </summary>
    protected async Task<HttpClient> CreateSimulatedInstanceAsync()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:Postgres",
                SharedFixture.Postgres.ConnectionString
            );
            builder.UseSetting("ConnectionStrings:Redis", SharedFixture.Redis.ConnectionString);
        });

        var client = factory.CreateClient();
        _simulatedInstances.Add((factory, client));
        return client;
    }

    // ─── API helpers ────────────────────────────────────────────────

    protected async Task<HttpResponseMessage> CreateProductAsync(
        string name = "Test Product",
        int initialStock = 100,
        decimal price = 29.99m
    )
    {
        return await Client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name,
                initialStock,
                price,
            }
        );
    }

    protected async Task<HttpResponseMessage> DeductStockAsync(
        Guid productId,
        int quantity,
        string strategy
    )
    {
        return await Client.PostAsJsonAsync(
            $"/api/inventory/{productId}/deduct",
            new { quantity, strategy }
        );
    }

    protected async Task<HttpResponseMessage> GetProductAsync(Guid productId)
    {
        return await Client.GetAsync($"/api/products/{productId}");
    }

    // ─── DB helpers ─────────────────────────────────────────────────

    protected async Task ExecuteDbContextAsync(Func<ECommerceDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<ECommerceDbContext>
        >();
        using var db = dbFactory.CreateDbContext();
        await action(db);
    }

    protected async Task<T> ExecuteDbContextAsync<T>(Func<ECommerceDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<ECommerceDbContext>
        >();
        using var db = dbFactory.CreateDbContext();
        return await action(db);
    }
}
