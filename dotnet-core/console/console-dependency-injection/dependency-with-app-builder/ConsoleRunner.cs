using Microsoft.Extensions.Options;

namespace Dependency.With.Host.Builder;

public class ConsoleRunner(IOptions<AppOptions> options, MyService service)
{
    private readonly AppOptions _options = options.Value;

    public Task ExecuteAsync()
    {
        Console.WriteLine($"Starting '{_options.ApplicationName}' App...");

        service.DoSomething();

        Console.ReadKey();

        Console.WriteLine("Application Stopped.");

        return Task.CompletedTask;
    }
}
