﻿using Dependency.Without.Host.Builder;
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
    // Create service collection
    IServiceCollection services = new ServiceCollection();

    // setup logs
    // https://github.com/serilog/serilog-extensions-hosting
    // https://github.com/serilog/serilog-aspnetcore#two-stage-initialization
    // Routes framework log messages through Serilog - get other sinks from top level definition
    services.AddSerilog((sp, loggerConfiguration) =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        
        // The downside of initializing Serilog in top level is that services from the ASP.NET Core host, including the appsettings.json configuration and dependency injection, aren't available yet.
        // setup sinks that related to `configuration` here instead of top level serilog configuration
        loggerConfiguration.ReadFrom.Configuration(configuration);
    });

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