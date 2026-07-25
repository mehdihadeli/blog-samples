using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Shared.Factory;

public class CustomWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    public Dictionary<string, string> Settings { get; } = new();
    public List<Action<IServiceCollection>> TestServiceConfigurations { get; } = new();
    public Dictionary<string, string> Environment { get; } = new();

    public CustomWebApplicationFactory<TEntryPoint> WithSetting(string key, string value)
    {
        Settings[key] = value;
        return this;
    }

    public CustomWebApplicationFactory<TEntryPoint> WithEnvironment(string name, string value)
    {
        Environment[name] = value;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var kvp in Settings)
            builder.UseSetting(kvp.Key, kvp.Value);

        foreach (var kvp in Environment)
            builder.UseSetting(kvp.Key, kvp.Value);

        builder.ConfigureServices(services =>
        {
            foreach (var configure in TestServiceConfigurations)
                configure(services);
        });
    }
}
