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
    var builder = Host.CreateApplicationBuilder(args);
    
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);
    builder.Configuration.AddJsonFile(
        "appsettings.json",
        optional: true,
        reloadOnChange: true
    );
        
    // https://github.com/serilog/serilog-extensions-hosting
    // https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
    // Routes framework log messages through Serilog - get other sinks from top level definition
    builder.Services.AddSerilog((sp, loggerConfiguration) =>
    {
        // The downside of initializing Serilog in top level is that services from the ASP.NET Core host, including the appsettings.json configuration and dependency injection, aren't available yet.
        // setup sinks that related to `configuration` here instead of top level serilog configuration
        loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration);
    });
    
    builder.Services.AddOptions<AppOptions>().BindConfiguration(nameof(AppOptions));
    builder.Services.AddSingleton<MyService>();
    builder.Services.AddSingleton<ConsoleRunner>();

    // build our HostApplicationBuilder to IHost
    var host = builder.Build();

    // run our console app
    await host.ExecuteConsoleRunner();
    // Or
    // await AppConsoleRunner.RunAsync(host);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}