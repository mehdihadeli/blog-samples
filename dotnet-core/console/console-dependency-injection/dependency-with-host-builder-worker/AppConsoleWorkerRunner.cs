using Microsoft.Extensions.Options;

namespace Dependency.With.Host.Builder.Worker;

public class AppConsoleWorkerRunner : IHostedService
{
	private readonly ILogger<AppConsoleWorkerRunner> _logger;
	private readonly IHostApplicationLifetime _appLifetime;
	private readonly MyService _service;
	private readonly AppOptions _options;

	public AppConsoleWorkerRunner(
		ILogger<AppConsoleWorkerRunner> logger,
		IHostApplicationLifetime appLifetime,
		IOptions<AppOptions> options,
		MyService service)
	{
		_logger = logger;
		_appLifetime = appLifetime;
		_service = service;
		_options = options.Value;
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_appLifetime.ApplicationStopped.Register(
			() =>
			{
				Console.WriteLine("Application Stopped.");
			});

		Console.WriteLine($"Starting '{_options.ApplicationName}' App...");

		_service.DoSomething();

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}