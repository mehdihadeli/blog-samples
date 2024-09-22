using Microsoft.Extensions.Options;

namespace Dependency.With.Host.Builder.Worker;

public class AppConsoleWorkerRunner(
	ILogger<AppConsoleWorkerRunner> logger,
	IHostApplicationLifetime appLifetime,
	IOptions<AppOptions> options,
	MyService service)
	: IHostedService
{
	private readonly AppOptions _options = options.Value;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		appLifetime.ApplicationStopped.Register(
			() =>
			{
				Console.WriteLine("Application Stopped.");
			});

		Console.WriteLine($"Starting '{_options.ApplicationName}' App...");

		service.DoSomething();

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}