using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace Tests.Shared.Fixtures;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string LocalPostgresImage = "postgres:17";
    private Respawner? _respawner;

    public PostgreSqlContainer Container { get; } =
        new PostgreSqlBuilder()
            .WithImage(LocalPostgresImage)
            .WithDatabase("sample_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public string ConnectionString => Container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        if (_respawner is null)
        {
            try
            {
                _respawner = await Respawner.CreateAsync(
                    connection,
                    new RespawnerOptions { DbAdapter = DbAdapter.Postgres }
                );
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("No tables found", StringComparison.Ordinal))
            {
                return;
            }
        }

        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
