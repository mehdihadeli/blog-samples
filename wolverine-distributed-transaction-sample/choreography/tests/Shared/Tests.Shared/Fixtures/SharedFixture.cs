using Tests.Shared.Fixtures;

namespace Tests.Shared;

public class SharedFixture : IAsyncLifetime
{
    public PostgresContainerFixture PostgresFixture { get; } = new();
    public RabbitMqContainerFixture RabbitMqFixture { get; } = new();

    public async Task ResetAsync()
    {
        await PostgresFixture.ResetAsync();
        await RabbitMqFixture.CleanupQueuesAsync();
    }

    public async ValueTask InitializeAsync()
    {
        await PostgresFixture.InitializeAsync();
        await RabbitMqFixture.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await PostgresFixture.DisposeAsync();
        await RabbitMqFixture.DisposeAsync();
    }
}
