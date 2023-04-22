using Dependency.With.Host.Builder;
using Dependency.With.Host.Builder.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        // .ConfigureLogging(logging =>
        // {
        //     logging.AddConsole();
        //     logging.AddDebug();
        // ... some other configurations for logs
        // })
        .UseSerilog(
            (context, sp, loggerConfiguration) =>
            {
                loggerConfiguration.Enrich
                    .WithProperty("Application", context.HostingEnvironment.ApplicationName)
                    .ReadFrom.Configuration(context.Configuration, sectionName: "Serilog")
                    .WriteTo.Console(
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level} - {Message:lj}{NewLine}{Exception}"
                    );
            }
        )
        // setup configurations - CreateDefaultBuilder do this for us, but we can override that configuration
        .ConfigureAppConfiguration(configurationBuilder =>
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
                // setup dependencies
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
