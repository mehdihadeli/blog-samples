using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared.Factory;
using Xunit.Sdk;

namespace Tests.Shared.Fixtures;

// https://wolverinefx.net/guide/testing.html
// https://jeremydmiller.com/2022/12/12/introducing-wolverine-for-effective-server-side-net-development/
// https://jeremydmiller.com/2022/12/13/how-wolverine-allows-for-easier-testing/
/// <summary>
/// Service-level test fixture. Boots the real application entry point
/// (<typeparamref name="TEntryPoint"/>) in-process via
/// <see cref="CustomWebApplicationFactory{TEntryPoint}"/> and overrides
/// configuration so the service uses Testcontainers-backed brokers/databases.
/// The container instances, per-test reset and the <c>TrackActivity</c>
/// assertions live in <see cref="SharedFixtureCore"/>.
/// </summary>
public abstract class SharedFixture<TEntryPoint>(
    bool usePostgres = false,
    bool useRabbitMq = false,
    bool useKafka = false,
    bool useMongo = false
) : SharedFixtureCore(usePostgres, useRabbitMq, useKafka, useMongo)
    where TEntryPoint : class
{
    private readonly IMessageSink? _messageSink;
    private CustomWebApplicationFactory<TEntryPoint>? _factory;

    private IServiceProvider? _serviceProvider;
    private IHttpContextAccessor? _httpContextAccessor;
    private HttpClient? _guestClient;

    public override IServiceProvider ServiceProvider => _serviceProvider ??= Factory.Services;

    public IHttpContextAccessor HttpContextAccessor =>
        _httpContextAccessor ??= ServiceProvider.GetRequiredService<IHttpContextAccessor>();

    public HttpClient GuestClient
    {
        get
        {
            if (_guestClient == null)
            {
                _guestClient = Factory.CreateClient();
                // Set the media type of the request to JSON - we need this for getting problem details result for all http calls because problem details just return response for request with media type JSON
                _guestClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                );
            }

            return _guestClient;
        }
    }

    private CustomWebApplicationFactory<TEntryPoint> Factory => _factory ??= CreateTestFactory();

    protected SharedFixture(IMessageSink messageSink)
        : this()
    {
        _messageSink = messageSink;
        _factory = CreateTestFactory();
    }

    public override async ValueTask DisposeAsync()
    {
        _factory?.Dispose();

        await base.DisposeAsync();
    }

    private CustomWebApplicationFactory<TEntryPoint> CreateTestFactory()
    {
        var factory = new CustomWebApplicationFactory<TEntryPoint>();

        factory.WithTestConfigureServices(ApplyTestConfigureServices);
        factory.WithTestConfigureAppConfiguration(ApplyTestConfigureAppConfiguration);
        factory.WithTestConfiguration(ApplyTestConfiguration);
        factory.AddOverrideEnvKeyValues(ApplyOverrideEnvKeyValues);
        factory.AddOverrideInMemoryConfig(ApplyOverrideInMemoryConfig);

        return factory;
    }

    protected virtual void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary) { }

    protected virtual void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary) { }

    protected virtual void ApplyTestConfiguration(IConfiguration configuration) { }

    protected virtual void ApplyTestConfigureAppConfiguration(
        WebHostBuilderContext context,
        IConfigurationBuilder builder
    ) { }

    protected virtual void ApplyTestConfigureServices(IServiceCollection collection) { }
}
