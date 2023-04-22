using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dependency.Without.Host.Builder;

public static class AppConsoleRunner
{
    public static Task RunAsync(IServiceProvider serviceProvider)
    {
        // run our console app
        var appOptions = serviceProvider.GetRequiredService<IOptions<AppOptions>>();
        Console.WriteLine($"Starting '{appOptions.Value.ApplicationName}' App...");

        var myService = serviceProvider.GetRequiredService<MyService>();
        myService.DoSomething();

        Console.ReadKey();

        Console.WriteLine("Application Stopped.");

        return Task.CompletedTask;
    }
}
