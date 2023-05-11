using Dependency.With.Host.Builder.Worker;
using Serilog;
using Serilog.Events;

// https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
Log.Logger = new LoggerConfiguration().MinimumLevel
	.Override("Microsoft", LogEventLevel.Information)
	.WriteTo.Console()
	.CreateBootstrapLogger();

// Load some envs like `ASPNETCORE_ENVIRONMENT` for accessing in `HostingEnvironment`, default is Production
DotNetEnv.Env.TraversePath().Load();

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
					.WriteTo.Console(
						outputTemplate:
						"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level} - {Message:lj}{NewLine}{Exception}");
			})
		// setup configurations - CreateDefaultBuilder do this for us, but we can override that configuration
		.ConfigureAppConfiguration(
			configurationBuilder =>
			{
				configurationBuilder.AddJsonFile(
					"appsettings.json",
					optional: true,
					reloadOnChange: true);
				configurationBuilder.AddEnvironmentVariables();
				configurationBuilder.AddCommandLine(args);
			})
		.ConfigureServices(
			(hostContext, services) =>
			{
				var configuration = hostContext.Configuration;
				var environment = hostContext.HostingEnvironment;
				var appOptions = configuration.GetSection("AppOptions").Get<AppOptions>();

				// setup dependencies
				services.AddOptions<AppOptions>().BindConfiguration(nameof(AppOptions));
				services.AddSingleton<MyService>();
				services.AddHostedService<AppConsoleWorkerRunner>();
			});

	// internally do `Build().RunAsync()`
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