using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tests.Shared.Fixtures;

namespace Tests.Shared.TestBase;

public abstract class IntegrationTestBase<TEntryPoint, TSharedFixture>(TSharedFixture sharedFixture)
    : IAsyncLifetime
    where TEntryPoint : class
    where TSharedFixture : SharedFixture<TEntryPoint>
{
    protected IServiceScope Scope => field ??= SharedFixture.ServiceProvider.CreateScope();

    protected TSharedFixture SharedFixture { get; } = sharedFixture;

    public virtual async ValueTask InitializeAsync()
    {
        await SharedFixture.ResetAsync();
        await ResetStateAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        Scope.Dispose();
    }

    protected virtual Task ResetStateAsync() => Task.CompletedTask;

    protected async Task ExecuteDbContextAsync<TContext>(Func<TContext, Task> action)
        where TContext : DbContext
    {
        await using var scope = SharedFixture.ServiceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await EnsureSchemaCreatedAsync(dbContext);
        await action(dbContext);
    }

    protected async Task<TResult> ExecuteDbContextAsync<TContext, TResult>(
        Func<TContext, Task<TResult>> action
    )
        where TContext : DbContext
    {
        await using var scope = SharedFixture.ServiceProvider.CreateAsyncScope();
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
