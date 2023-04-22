using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PublisherConfirm;
using PublisherConfirm.Contracts;
using Serilog;
using Serilog.Events;

// https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
Log.Logger = new LoggerConfiguration().MinimumLevel
	.Override("Microsoft", LogEventLevel.Information)
	.WriteTo.Console()
	.CreateBootstrapLogger();

try
{
	// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host#default-builder-settings
	var hostBuilder = Host.CreateDefaultBuilder(args);

	hostBuilder
		.UseSerilog(
			(context, sp, loggerConfiguration) =>
			{
				loggerConfiguration.Enrich
					.WithProperty("Application", context.HostingEnvironment.ApplicationName)
					.ReadFrom.Configuration(context.Configuration, sectionName: "Serilog")
					.WriteTo.Console();
			})
		.ConfigureServices(
			(hostContext, services) =>
			{
				// setup dependencies
				services.AddSingleton<IPublisher, Publisher>();
				services.AddOptions<RabbitMqOptions>().BindConfiguration(nameof(RabbitMqOptions));
				services.AddHostedService<ConsoleRunnerWorker>();
			});
	// build our HostBuilder and Run it internally
	await hostBuilder.RunConsoleAsync();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
	Log.CloseAndFlush();
}