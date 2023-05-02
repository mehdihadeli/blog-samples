using Dependency.With.Minimal.Host.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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
	// https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60?tabs=visual-studio#new-hosting-model
	var builder = WebApplication.CreateBuilder(args);

	// setup logs
	builder.Host.UseSerilog(
		(context, sp, loggerConfiguration) =>
		{
			loggerConfiguration.Enrich
				.WithProperty("Application", context.HostingEnvironment.ApplicationName)
				.ReadFrom.Configuration(context.Configuration, sectionName: "Serilog")
				.WriteTo.Console(
					outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level} - {Message:lj}{NewLine}{Exception}");
		});

	// builder.Host.ConfigureLogging(
	// 	logging =>
	// 	{
	// 		logging.AddConsole();
	// 		logging.AddDebug();
	// 		//... some other configurations for logs
	// 	});

	// setup dependencies
	builder.Services.AddSingleton<MyService>();
	builder.Services.AddOptions<AppOptions>().BindConfiguration(nameof(AppOptions));

	// build our WebApplicationBuilder to WebApplication
	var app = builder.Build();

	// run our console app
	await AppConsoleRunner.RunAsync(app);
}
catch (Exception ex)
{
	Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
	Log.CloseAndFlush();
}
