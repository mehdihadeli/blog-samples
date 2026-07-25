using Testcontainers.RabbitMq;

namespace Tests.Shared.Fixtures;

public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-management")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async Task CleanupQueuesAsync()
    {
        // In a real test suite, purge all queues between tests
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
