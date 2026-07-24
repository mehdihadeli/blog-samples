using ECommerce.IntegrationTests.Fixtures;

namespace ECommerce.IntegrationTests;

public sealed class ECommerceSharedFixture : IAsyncLifetime
{
    public PostgresContainerFixture Postgres { get; } = new();
    public RedisContainerFixture Redis { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await Postgres.InitializeAsync();
        await Redis.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Redis.DisposeAsync();
        await Postgres.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        await Postgres.ResetAsync();
        await Redis.FlushAsync();
    }
}
