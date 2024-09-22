using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dependency.With.Host.Builder.Extensions;

public static class Extensions
{
    public static async ValueTask ExecuteConsoleRunner(this IHost host)
    {
        var runners = host.Services.GetServices<ConsoleRunner>().ToList();
        if (runners.Any() == false)
            throw new Exception(
                "Console runner not found, create a console runner with implementing 'IConsoleRunner' interface"
            );

        if (runners.Count > 1)
            throw new Exception("Console app should have just one runner.");

        var runner = runners.First();

        await runner.ExecuteAsync();
    }
}
