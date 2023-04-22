using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dependency.With.Minimal.Host.Builder;

public static class AppConsoleRunner
{
	public static Task RunAsync(WebApplication app)
	{
		// run our console app
		var appOptions = app.Services.GetRequiredService<IOptions<AppOptions>>();
		Console.WriteLine($"Starting '{appOptions.Value.ApplicationName}' App...");

		var myService = app.Services.GetRequiredService<MyService>();
		myService.DoSomething();

		Console.ReadKey();

		Console.WriteLine("Application Stopped.");

		return Task.CompletedTask;
	}
}