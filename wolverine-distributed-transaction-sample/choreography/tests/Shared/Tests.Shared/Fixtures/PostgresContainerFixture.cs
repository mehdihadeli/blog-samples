using Testcontainers.PostgreSql;

namespace Tests.Shared.Fixtures;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async Task ResetAsync()
    {
        // Respawn logic can go here to clear all tables between tests
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
