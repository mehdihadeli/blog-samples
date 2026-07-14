using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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

    protected KafkaContainerFixture Kafka => SharedFixture.Kafka;

    protected virtual string MessagingTransport => "rabbitmq";

    protected CustomWebApplicationFactory<TEntryPoint> Factory =>
        _factory
        ?? throw new InvalidOperationException(
            "The test application factory is not initialized for the current test."
        );

    public virtual async Task InitializeAsync()
    {
        await SharedFixture.ResetAsync(MessagingTransport);
        _factory = SharedFixture.CreateFactory(MessagingTransport, ConfigureFactory);
        await ResetStateAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }
    }

    protected virtual void ConfigureFactory(CustomWebApplicationFactory<TEntryPoint> factory) { }

    protected virtual Task ResetStateAsync() => Task.CompletedTask;

    protected Task ShouldPublish<T>()
        where T : class
    {
        return SharedFixture.ShouldPublish<T>();
    }

    protected Task ShouldConsume<T>()
        where T : class
    {
        return SharedFixture.ShouldConsume<T>();
    }

    protected Task ShouldConsume<TMessage, TConsumedBy>()
        where TMessage : class
        where TConsumedBy : class
    {
        return SharedFixture.ShouldConsume<TMessage, TConsumedBy>();
    }

    protected async Task ExecuteDbContextAsync<TContext>(Func<TContext, Task> action)
        where TContext : DbContext
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await EnsureSchemaCreatedAsync(dbContext);
        await action(dbContext);
    }

    protected async Task<TResult> ExecuteDbContextAsync<TContext, TResult>(
        Func<TContext, Task<TResult>> action
    )
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
        {
            await dbContext.Database.EnsureCreatedAsync();
            return;
        }

        await dbContext.Database.EnsureCreatedAsync();

        try
        {
            await databaseCreator.CreateTablesAsync();
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.DuplicateTable)
        {
            // EF tables for this context already exist.
        }
    }
}
