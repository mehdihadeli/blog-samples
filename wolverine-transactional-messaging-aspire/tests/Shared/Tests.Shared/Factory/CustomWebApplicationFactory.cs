using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Tests.Shared.Factory;

public sealed class CustomWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly Dictionary<string, string?> _settings = new(StringComparer.OrdinalIgnoreCase);

    public CustomWebApplicationFactory<TEntryPoint> WithSetting(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        foreach (var setting in _settings)
        {
            builder.UseSetting(setting.Key, setting.Value);
        }

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_settings));

        base.ConfigureWebHost(builder);
    }
}
