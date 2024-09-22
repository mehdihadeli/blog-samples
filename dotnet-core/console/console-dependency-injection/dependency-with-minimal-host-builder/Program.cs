using Dependency.With.Minimal.Host.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

// https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
// https://github.com/serilog/serilog-extensions-hosting
Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
	.Enrich.FromLogContext()
	.WriteTo.Console(
		outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level} - {Message:lj}{NewLine}{Exception}"
	)
	.CreateBootstrapLogger();


// Load some envs like `ASPNETCORE_ENVIRONMENT` for accessing in `HostingEnvironment`, default is Production
DotNetEnv.Env.TraversePath().Load();

try
{
	// https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60?tabs=visual-studio#new-hosting-model
	var builder = WebApplication.CreateBuilder(args);

	
	// setup logs
	// https://github.com/serilog/serilog-extensions-hosting
	// https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
	// Routes framework log messages through Serilog - get other sinks from top level definition
	builder.Services.AddSerilog((sp, loggerConfiguration) =>
	{
		// The downside of initializing Serilog in top level is that services from the ASP.NET Core host, including the appsettings.json configuration and dependency injection, aren't available yet.
		// setup sinks that related to `configuration` here instead of top level serilog configuration
		loggerConfiguration.ReadFrom.Configuration(builder.Configuration);
	});

	var configuration = builder.Configuration;
	var environment = builder.Environment;
	var appOptions = configuration.GetSection("AppOptions").Get<AppOptions>();

	// setup dependencies
	builder.Services.AddSingleton<ConsoleRunner>();
	builder.Services.AddSingleton<MyService>();
	builder.Services.AddOptions<AppOptions>().BindConfiguration(nameof(AppOptions));

	// build our WebApplicationBuilder to WebApplication
	var app = builder.Build();

	// run our console app
	await AppConsoleRunner.RunAsync(app);
	// Or
	// await app.ExecuteConsoleRunner();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
	Log.CloseAndFlush();
}
