using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace Tests.Shared.Fixtures;

public sealed class MongoContainerFixture : IAsyncLifetime
{
    private const string LocalMongoImage = "mongo:8.0";
    private const string DatabaseName = "catalogs_tests";

    public MongoDbContainer Container { get; } =
        new MongoDbBuilder().WithImage(LocalMongoImage).Build();

    public string ConnectionString
    {
        get
        {
            var builder = new MongoUrlBuilder(Container.GetConnectionString())
            {
                DatabaseName = DatabaseName,
                AuthenticationSource = "admin",
            };

            return builder.ToString();
        }
    }

    public async ValueTask InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        var client = new MongoClient(ConnectionString);
        await client.DropDatabaseAsync(DatabaseName, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
