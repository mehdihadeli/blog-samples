using Tests.Shared.Fixtures;

namespace Tests.Shared.TestBase;

/// <summary>
/// Base class for building-block integration tests (isolated building-block
/// modules tested against a real broker, without any microservice).
/// Building-block suites delete broker state between tests — Kafka topics and
/// RabbitMQ queues via <c>ResetAsync</c> — so a cached test host never
/// re-provisions the deleted topology and only the first test sees it. This
/// base restarts the host in its own
/// <see cref="InitializeAsync"/> so Wolverine re-runs AutoProvision against the
/// cleaned broker before every test.
/// Microservice suites (which reuse one host across tests) should keep using
/// <see cref="IntegrationTestBase{TEntryPoint,TSharedFixture}"/> directly.
/// </summary>
public abstract class BuildingBlocksIntegrationTestBase<TEntryPoint, TSharedFixture>(
    TSharedFixture sharedFixture
) : IntegrationTestBase<TEntryPoint, TSharedFixture>(sharedFixture)
    where TEntryPoint : class
    where TSharedFixture : SharedFixture<TEntryPoint>
{
    /// <summary>
    /// Drop the cached host first, then run the standard reset (broker cleanup +
    /// host start). The host restart must happen BEFORE <c>ResetAsync</c> starts
    /// the host, otherwise the freshly provisioned topics get deleted right after
    /// they are created.
    /// </summary>
    public override async ValueTask InitializeAsync()
    {
        SharedFixture.ResetCachedHost();
        await base.InitializeAsync();
    }
}
