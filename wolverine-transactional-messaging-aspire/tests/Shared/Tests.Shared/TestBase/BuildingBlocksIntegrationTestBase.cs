using Tests.Shared.Fixtures;

namespace Tests.Shared.TestBase;

/// <summary>
/// Base class for building-block integration tests. No entry-point type is
/// needed because <see cref="BuildingBlocksSharedFixture"/> boots its own host
/// via <c>WebApplication.CreateBuilder</c> — unlike
/// <see cref="IntegrationTestBase{TEntryPoint, TSharedFixture}"/>, which hosts
/// a real service entry point through <c>WebApplicationFactory&lt;T&gt;</c>.
/// </summary>
public abstract class BuildingBlocksIntegrationTestBase<TSharedFixture>(
    TSharedFixture sharedFixture
) : IAsyncLifetime
    where TSharedFixture : BuildingBlocksSharedFixture
{
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
    /// already hard-cancelled by <see cref="BuildingBlocksSharedFixture.TestTimeout"/>.
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

        // Cap per-test reset (broker purge / topic deletion) at TestTimeout so a
        // stuck broker reset fails fast instead of hanging the run.
        await SharedFixture
            .ResetAsync(TestCancellationToken)
            .WaitAsync(SharedFixture.TestTimeout, TestCancellationToken);

        await ResetStateAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        _testTimeoutCts?.Cancel();
        _testTimeoutCts?.Dispose();
        _testTimeoutCts = null;
    }

    protected virtual Task ResetStateAsync() => Task.CompletedTask;
}
