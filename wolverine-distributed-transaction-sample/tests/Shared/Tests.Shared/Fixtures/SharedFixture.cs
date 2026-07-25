using Tests.Shared.Factory;
using Xunit;

namespace Tests.Shared.Fixtures;

public abstract class SharedFixture<TEntryPoint> : IAsyncLifetime
    where TEntryPoint : class
{
    protected SharedFixture() { }

    public PostgresContainerFixture Postgres { get; } = new();
    public RabbitMqContainerFixture RabbitMq { get; } = new();

    public virtual async ValueTask InitializeAsync()
    {
        await Postgres.InitializeAsync();
        await RabbitMq.InitializeAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await Postgres.DisposeAsync();
    }

    public async Task ResetAsync(string transport, CancellationToken cancellationToken = default)
    {
        await Postgres.ResetAsync();
        await RabbitMq.EnsureStartedAsync();
        await RabbitMq.CleanupQueuesAsync(cancellationToken);
    }

    public CustomWebApplicationFactory<TEntryPoint> CreateFactory(
        string transport,
        Action<CustomWebApplicationFactory<TEntryPoint>>? configure = null)
    {
        var factory = new CustomWebApplicationFactory<TEntryPoint>();

        // Always set DB connection strings (containers start eagerly)
        factory.WithSetting("ConnectionStrings:ordersdb", Postgres.ConnectionString);
        factory.WithSetting("Messaging:Transport", transport);

        // Only set the active broker connection string
        factory.WithSetting("ConnectionStrings:rabbitmq", RabbitMq.ConnectionString);

        ConfigureFactory(factory, transport);
        configure?.Invoke(factory);
        return factory;
    }

    /// <summary>
    /// Optional per-test-project factory customisation.
    /// Called after default connection strings are set.
    /// </summary>
    protected virtual void ConfigureFactory(
        CustomWebApplicationFactory<TEntryPoint> factory, string transport) { }
}
