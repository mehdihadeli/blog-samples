using StackExchange.Redis;
using Testcontainers.Redis;

namespace ECommerce.IntegrationTests.Fixtures;

public sealed class RedisContainerFixture : IAsyncLifetime
{
    private const string LocalRedisImage = "redis:7.4";
    private const int RedisPort = 6379;

    public RedisContainer Container { get; } =
        new RedisBuilder().WithImage(LocalRedisImage).Build();

    public string ConnectionString => Container.GetConnectionString();

    /// <summary>
    /// Returns a fully connected ConnectionMultiplexer pointing at this container.
    /// Reused across tests in the same collection.
    /// </summary>
    private ConnectionMultiplexer? _multiplexer;

    /// <summary>
    /// The mapped public port for Redis (6379/tcp).
    /// Available only after the container is started.
    /// </summary>
    private int _mappedPort;

    public ConnectionMultiplexer Multiplexer =>
        _multiplexer
        ?? throw new InvalidOperationException(
            "Multiplexer not initialized. Call InitializeAsync first."
        );

    public async ValueTask InitializeAsync()
    {
        await Container.StartAsync();
        _mappedPort = Container.GetMappedPublicPort(RedisPort);

        // Warm up the multiplexer so tests don't pay connection latency
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(
            new ConfigurationOptions
            {
                EndPoints = { { Container.Hostname, _mappedPort } },
                AbortOnConnectFail = false,
                ConnectTimeout = 5000,
                ConnectRetry = 3,
            }
        );
    }

    public async Task FlushAsync()
    {
        // Clear all Redis data between tests
        var server = Multiplexer.GetServer(Container.Hostname, _mappedPort);
        await server.FlushDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _multiplexer?.Dispose();
        await Container.DisposeAsync();
    }
}
