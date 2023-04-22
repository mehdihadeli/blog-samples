using Microsoft.Extensions.Options;

namespace Dependency.With.Host.Builder;

public class ConsoleRunner
{
    private readonly MyService _service;
    private readonly AppOptions _options;

    public ConsoleRunner(IOptions<AppOptions> options, MyService service)
    {
        _service = service;
        _options = options.Value;
    }

    public Task ExecuteAsync()
    {
        Console.WriteLine($"Starting '{_options.ApplicationName}' App...");

        _service.DoSomething();

        Console.ReadKey();

        Console.WriteLine("Application Stopped.");

        return Task.CompletedTask;
    }
}
