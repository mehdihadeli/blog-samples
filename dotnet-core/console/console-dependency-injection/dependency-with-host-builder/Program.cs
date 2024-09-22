using Dependency.With.Host.Builder;
using Dependency.With.Host.Builder.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host#default-builder-settings
    var hostBuilder = Host.CreateDefaultBuilder(args);
    
    // setup configurations - CreateDefaultBuilder do this for us, but we can override that configuration
    hostBuilder.ConfigureAppConfiguration(configurationBuilder =>
        {
            configurationBuilder.AddJsonFile(
                "appsettings.json",
                optional: true,
                reloadOnChange: true
            );
            configurationBuilder.AddEnvironmentVariables();
            configurationBuilder.AddCommandLine(args);
        })
        .ConfigureServices(
            (hostContext, services) =>
            {
                var configuration = hostContext.Configuration;
                var environment = hostContext.HostingEnvironment;
                var appOptions = configuration.GetSection("AppOptions").Get<AppOptions>();

                // https://github.com/serilog/serilog-extensions-hosting
                // https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
                // Routes framework log messages through Serilog - get other sinks from top level definition
                services.AddSerilog((sp, loggerConfiguration) =>
                {
                    // The downside of initializing Serilog in top level is that services from the ASP.NET Core host, including the appsettings.json configuration and dependency injection, aren't available yet.
                    // setup sinks that related to `configuration` here instead of top level serilog configuration
                    loggerConfiguration
                        .ReadFrom.Configuration(configuration);
                });
                services.AddOptions<AppOptions>().BindConfiguration(nameof(AppOptions));
                services.AddSingleton<MyService>();
                services.AddSingleton<ConsoleRunner>();
            }
        );
    // build our HostBuilder to IHost
    var host = hostBuilder.Build();

    // run our console app
    await host.ExecuteConsoleRunner();
    // Or
    // await AppConsoleRunner.RunAsync(host.Services);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}