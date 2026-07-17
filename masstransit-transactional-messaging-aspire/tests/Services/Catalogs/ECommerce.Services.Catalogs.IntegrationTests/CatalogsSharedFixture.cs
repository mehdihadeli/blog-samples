using ECommerce.Services.Catalogs.Shared.Data;
using Npgsql;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;

namespace ECommerce.Services.Catalogs.IntegrationTests;

/// <summary>
/// Collection-scoped shared fixture for the Catalogs integration test suite.
/// Manages Postgres, RabbitMQ, Kafka and MongoDB container lifecycles and
/// creates WebApplicationFactory instances pre-configured with the correct
/// connection strings for each transport.
/// </summary>
public sealed class CatalogsSharedFixture : SharedFixture<Program>
{
    public CatalogsSharedFixture()
        : base(useMongo: true) { }

    public string MongoConnectionString =>
        Mongo?.ConnectionString
        ?? throw new InvalidOperationException("MongoDB fixture not configured.");

    public Task ExecuteCatalogsDbContextAsync(Func<CatalogsDbContext, Task> action) =>
        ExecuteCatalogsDbContextInternalAsync(action);

    public Task<TResult> ExecuteCatalogsDbContextAsync<TResult>(
        Func<CatalogsDbContext, Task<TResult>> action
    ) => ExecuteCatalogsDbContextInternalAsync(action);

    public async Task<int> CountOutgoingEnvelopeRowsAsync(string destinationLike)
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            select count(*)
            from catalogs.outbox_messages
            where destination like @destinationLike
            """,
            connection
        );
        command.Parameters.AddWithValue("destinationLike", destinationLike);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task ExecuteCatalogsDbContextInternalAsync(Func<CatalogsDbContext, Task> action)
    {
        using var factory = CreateFactory(DefaultTransport);
        await ExecuteDbContextAsync(factory, action);
    }

    private async Task<TResult> ExecuteCatalogsDbContextInternalAsync<TResult>(
        Func<CatalogsDbContext, Task<TResult>> action
    )
    {
        using var factory = CreateFactory(DefaultTransport);
        return await ExecuteDbContextAsync(factory, action);
    }

    protected override void ConfigureFactory(
        CustomWebApplicationFactory<Program> factory,
        string transport
    )
    {
        factory
            .WithSetting("Messaging:Transport", transport)
            .WithSetting("ConnectionStrings:catalogsdb", Postgres.ConnectionString)
            .WithSetting("ConnectionStrings:catalogs-mongo", MongoConnectionString);

        if (string.Equals(transport, "kafka", StringComparison.OrdinalIgnoreCase))
        {
            factory.WithSetting("ConnectionStrings:kafka", Kafka.BootstrapServers);
            return;
        }

        factory.WithSetting("ConnectionStrings:rabbitmq", RabbitMq.ConnectionString);
    }
}
