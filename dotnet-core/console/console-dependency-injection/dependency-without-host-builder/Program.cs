using Dependency.Without.Host.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

// Load some envs like `ASPNETCORE_ENVIRONMENT`
DotNetEnv.Env.TraversePath().Load();

IConfiguration configuration = new ConfigurationBuilder()
	.AddJsonFile("appsettings.json", optional: false)
	.Build();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
	.Enrich.WithProperty("Application", "ConsoleAppWithoutHostBuilder")
	.ReadFrom.Configuration(configuration, sectionName: "Serilog")
	.WriteTo.Console(
		outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level} - {Message:lj}{NewLine}{Exception}"
	).CreateLogger();

try
{
	// Create service collection
	IServiceCollection services = new ServiceCollection();

	// setup dependencies
	// Add logging
	services.AddLogging(
		loggingBuilder =>
		{
			loggingBuilder.ClearProviders();
			loggingBuilder.AddSerilog(dispose: true, logger: Log.Logger);
		});
	// Add Configuration
	services.AddSingleton(configuration);
	services.AddOptions<AppOptions>().BindConfiguration(nameof(AppOptions));
	services.AddTransient<MyService>();

	// Build service provider
	IServiceProvider serviceProvider = services.BuildServiceProvider();

	// Run the console app
	await AppConsoleRunner.RunAsync(serviceProvider);
}
catch (Exception ex)
{
	Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
	Log.CloseAndFlush();
}