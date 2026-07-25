using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;
using Xunit;

namespace Tests.Shared.TestBase;

public abstract class IntegrationTestBase<TEntryPoint, TSharedFixture> : IAsyncLifetime
    where TEntryPoint : class
    where TSharedFixture : SharedFixture<TEntryPoint>
{
    private CustomWebApplicationFactory<TEntryPoint>? _factory;

    protected IntegrationTestBase(TSharedFixture sharedFixture)
    {
        SharedFixture = sharedFixture;
    }

    protected TSharedFixture SharedFixture { get; }
    protected PostgresContainerFixture Postgres => SharedFixture.Postgres;
    protected RabbitMqContainerFixture RabbitMq => SharedFixture.RabbitMq;

    protected virtual string MessagingTransport => "rabbitmq";

    protected CustomWebApplicationFactory<TEntryPoint> Factory =>
        _factory ?? throw new InvalidOperationException(
            "Factory not initialised. Ensure InitializeAsync is called before accessing Factory.");

    public virtual async ValueTask InitializeAsync()
    {
        await SharedFixture.ResetAsync(MessagingTransport);
        _factory = SharedFixture.CreateFactory(MessagingTransport, ConfigureFactory);
        await ResetStateAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }
    }

    protected virtual void ConfigureFactory(CustomWebApplicationFactory<TEntryPoint> factory) { }
    protected virtual Task ResetStateAsync() => Task.CompletedTask;

    protected async Task ExecuteDbContextAsync<TContext>(Func<TContext, Task> action)
        where TContext : DbContext
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await EnsureSchemaCreatedAsync(dbContext);
        await action(dbContext);
    }

    protected async Task<TResult> ExecuteDbContextAsync<TContext, TResult>(
        Func<TContext, Task<TResult>> action)
        where TContext : DbContext
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await EnsureSchemaCreatedAsync(dbContext);
        return await action(dbContext);
    }

    private static async Task EnsureSchemaCreatedAsync(DbContext dbContext)
    {
        var databaseCreator = dbContext.Database.GetService<IRelationalDatabaseCreator>();
        if (!await databaseCreator.ExistsAsync())
            await dbContext.Database.EnsureCreatedAsync();

        await dbContext.Database.EnsureCreatedAsync();

        try { await databaseCreator.CreateTablesAsync(); }
        catch { /* Tables may already exist; proceed. */ }
    }
}
