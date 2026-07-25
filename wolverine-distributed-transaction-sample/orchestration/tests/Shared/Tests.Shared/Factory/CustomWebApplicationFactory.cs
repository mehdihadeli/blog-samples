using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Shared.Factory;

public sealed class CustomWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly Dictionary<string, string?> _settings = new(StringComparer.OrdinalIgnoreCase);
    private Action<IServiceCollection>? _configureTestServices;
    private string _environment = "Test";

    public CustomWebApplicationFactory<TEntryPoint> WithSetting(string key, string? value)
    { _settings[key] = value; return this; }

    public CustomWebApplicationFactory<TEntryPoint> ConfigureTestServices(Action<IServiceCollection> configure)
    { _configureTestServices = configure; return this; }

    public CustomWebApplicationFactory<TEntryPoint> WithEnvironment(string environment)
    { _environment = environment; return this; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        foreach (var setting in _settings) builder.UseSetting(setting.Key, setting.Value);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_settings));
        if (_configureTestServices is not null) builder.ConfigureServices(_configureTestServices);
        base.ConfigureWebHost(builder);
    }
}
