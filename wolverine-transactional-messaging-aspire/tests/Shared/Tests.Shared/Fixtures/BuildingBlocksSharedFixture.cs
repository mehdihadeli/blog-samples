using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Shared.Fixtures;

// Mirrors how Wolverine's own transport tests boot a host right inside the
// test fixture (https://github.com/JasperFx/wolverine/blob/main/src/Transports/RabbitMQ/Wolverine.RabbitMQ.Tests/send_by_topics.cs):
// the fixture builds the host it needs instead of pointing a shared entry point
// at a shared test-host project.
/// <summary>
/// Base fixture for isolated building-block integration tests. Each building
/// block (RabbitMQ, Kafka, ...) is its own project and needs its own host
/// wiring — a shared test-host entry point does not scale. So this fixture
/// owns host creation via <c>WebApplication.CreateBuilder</c> and lets each
/// derived fixture register its dependencies (transport building block,
/// handlers, topology, connection strings) and middleware through
/// <see cref="ConfigureBuilder"/> / <see cref="ConfigurePipeline"/>.
/// </summary>
public abstract class BuildingBlocksSharedFixture(
    bool usePostgres = false,
    bool useRabbitMq = false,
    bool useKafka = false,
    bool useMongo = false
) : SharedFixtureCore(usePostgres, useRabbitMq, useKafka, useMongo)
{
    private WebApplication? _host;
    private IServiceProvider? _serviceProvider;

    /// <summary>The started test host (created in <see cref="InitializeAsync"/>).</summary>
    public WebApplication Host =>
        _host
        ?? throw new InvalidOperationException(
            "The test host has not been started yet. Did the fixture initialize?"
        );

    public override IServiceProvider ServiceProvider => _serviceProvider ??= Host.Services;

    /// <summary>
    /// Register the building block's dependencies on the host builder: the
    /// transport registration (e.g. <c>AddWolverineRabbitMq</c>), the handler
    /// assembly, the manual topology, and any connection strings the building
    /// block reads from configuration.
    /// </summary>
    protected abstract void ConfigureBuilder(WebApplicationBuilder builder);

    /// <summary>
    /// Optional middleware / endpoint pipeline. Default: none — building-block
    /// tests exercise message round-trips, not HTTP.
    /// </summary>
    protected virtual void ConfigurePipeline(WebApplication app) { }

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        var builder = WebApplication.CreateBuilder();

        // Deterministic environment (WebApplicationFactory defaults to Development
        // too) and an in-process test server so no port is bound during tests.
        builder.Environment.EnvironmentName = "Development";
        builder.WebHost.UseTestServer();

        builder.Services.AddHttpContextAccessor();

        ConfigureBuilder(builder);

        _host = builder.Build();

        ConfigurePipeline(_host);

        await _host.StartAsync().WaitAsync(TimeSpan.FromSeconds(30));
    }

    public override async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            await _host.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}
