using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Shared.Factory;

public sealed class CustomWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly Dictionary<string, string?> _inMemoryConfigs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _overrideEnvKeysToDispose = [];
    private Action<IServiceCollection>? _testConfigureServices;
    private Action<IConfiguration>? _testConfiguration;
    private Action<WebHostBuilderContext, IConfigurationBuilder>? _testConfigureAppConfiguration;
    private string _environment = "Development";

    public CustomWebApplicationFactory<TEntryPoint> WithTestConfigureServices(
        Action<IServiceCollection> services
    )
    {
        _testConfigureServices += services;
        return this;
    }

    public CustomWebApplicationFactory<TEntryPoint> WithTestConfiguration(
        Action<IConfiguration> configurations
    )
    {
        _testConfiguration += configurations;
        return this;
    }

    public CustomWebApplicationFactory<TEntryPoint> WithTestConfigureAppConfiguration(
        Action<WebHostBuilderContext, IConfigurationBuilder> appConfigurations
    )
    {
        _testConfigureAppConfiguration += appConfigurations;
        return this;
    }

    public CustomWebApplicationFactory<TEntryPoint> WithEnvironment(string environment)
    {
        _environment = environment;
        return this;
    }

    public CustomWebApplicationFactory<TEntryPoint> AddOverrideEnvKeyValues(
        Action<IDictionary<string, string>> keyValuesAction
    )
    {
        var keyValues = new Dictionary<string, string>();
        keyValuesAction.Invoke(keyValues);

        foreach (var (key, value) in keyValues)
        {
            _overrideEnvKeysToDispose.Add(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        return this;
    }

    public CustomWebApplicationFactory<TEntryPoint> AddOverrideInMemoryConfig(
        Action<IDictionary<string, string>> inmemoryConfigsAction
    )
    {
        var inmemoryConfigs = new Dictionary<string, string>();
        inmemoryConfigsAction.Invoke(inmemoryConfigs);

        foreach (var (key, value) in inmemoryConfigs)
        {
            _inMemoryConfigs.TryAdd(key, value);
        }

        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        builder.ConfigureAppConfiguration(
            (hostingContext, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(_inMemoryConfigs);
                _testConfiguration?.Invoke(hostingContext.Configuration);
                _testConfigureAppConfiguration?.Invoke(hostingContext, configurationBuilder);
            }
        );

        builder.ConfigureTestServices(services =>
        {
            _testConfigureServices?.Invoke(services);
        });

        base.ConfigureWebHost(builder);
    }
}
