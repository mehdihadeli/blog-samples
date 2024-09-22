using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dependency.With.Minimal.Host.Builder.Extensions;

public static class Extensions
{
    public static async ValueTask ExecuteConsoleRunner(this WebApplication app)
    {
        var runners = app.Services.GetServices<ConsoleRunner>().ToList();
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
