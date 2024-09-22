using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dependency.With.Host.Builder;

public static class AppConsoleRunner
{
    public static Task RunAsync(IHost host)
    {
        // run our console app
        var appOptions = host.Services.GetRequiredService<IOptions<AppOptions>>();
        Console.WriteLine($"Starting '{appOptions.Value.ApplicationName}' App...");

        var myService = host.Services.GetRequiredService<MyService>();
        myService.DoSomething();

        Console.ReadKey();

        Console.WriteLine("Application Stopped.");

        return Task.CompletedTask;
    }
}