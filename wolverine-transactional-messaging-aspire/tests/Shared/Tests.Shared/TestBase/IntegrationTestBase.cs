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

    private CancellationTokenSource? _testTimeoutCts;

    private static CancellationToken GetAmbientCancellationToken()
    {
        // TestContext.Current can be null when no test is running (e.g. fixture
        // construction), despite xUnit's non-nullable annotation.
        ITestContext? ambient = TestContext.Current;
        return ambient?.CancellationToken ?? CancellationToken.None;
    }

    /// <summary>
    /// Per-test cancellation token that fires 90 seconds after the test starts (or
    /// as soon as xUnit cancels the test). Pass it to HTTP calls, polling loops, and
    /// broker waits so no integration test can hang the run. Tracked sessions are
    /// already hard-cancelled by <see cref="SharedFixture{TEntryPoint}.TestTimeout"/>.
    /// </summary>
    protected CancellationToken TestCancellationToken =>
        _testTimeoutCts?.Token ?? GetAmbientCancellationToken();

    public virtual async ValueTask InitializeAsync()
    {
        _testTimeoutCts?.Dispose();
        _testTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            GetAmbientCancellationToken()
        );
        _testTimeoutCts.CancelAfter(SharedFixture.TestTimeout);

        // Cap per-test reset (container cleanup, topic deletion, queue purging) at
        // TestTimeout so a stuck broker reset fails fast instead of hanging the run.
        await SharedFixture
            .ResetAsync(TestCancellationToken)
            .WaitAsync(SharedFixture.TestTimeout, TestCancellationToken);

        await ResetStateAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        Scope.Dispose();

        _testTimeoutCts?.Cancel();
        _testTimeoutCts?.Dispose();
        _testTimeoutCts = null;
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
