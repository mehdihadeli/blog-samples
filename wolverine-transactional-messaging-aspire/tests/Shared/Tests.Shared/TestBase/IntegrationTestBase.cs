using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;
using Xunit;

namespace Tests.Shared.TestBase;

public abstract class IntegrationTestBase<TEntryPoint> : IAsyncLifetime
    where TEntryPoint : class
{
    protected PostgresContainerFixture Postgres { get; }

    protected RabbitMqContainerFixture RabbitMq { get; }

    protected KafkaContainerFixture Kafka { get; }

    protected CustomWebApplicationFactory<TEntryPoint> Factory { get; }

    protected IntegrationTestBase(
        PostgresContainerFixture postgres,
        RabbitMqContainerFixture rabbitMq,
        KafkaContainerFixture kafka
    )
    {
        Postgres = postgres;
        RabbitMq = rabbitMq;
        Kafka = kafka;
        Factory = new CustomWebApplicationFactory<TEntryPoint>();
    }

    public virtual async Task InitializeAsync()
    {
        await Postgres.ResetAsync();

        if (UsesKafkaTransport)
        {
            await Kafka.EnsureStartedAsync();
            await Kafka.CleanupTopicsAsync();
        }
        else
        {
            await RabbitMq.EnsureStartedAsync();
            await RabbitMq.CleanupQueuesAsync();
        }

        ConfigureFactory(Factory);
        await ResetStateAsync();
    }

    public virtual async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
    }

    protected virtual void ConfigureFactory(CustomWebApplicationFactory<TEntryPoint> factory) { }

    protected virtual Task ResetStateAsync() => Task.CompletedTask;

    protected abstract bool UsesKafkaTransport { get; }

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
