using System.Reflection;
using BuildingBlocks.Integration.Wolverine.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.RabbitMQ;

namespace BuildingBlocks.Integration.Wolverine;

public static class WolverineTransactionalExtensions
{
    /// <summary>
    /// Registers Wolverine with RabbitMQ transport, transactional outbox/inbox,
    /// and optional saga support.
    /// </summary>
    public static IHostBuilder AddTransactionalWolverine(
        this IHostBuilder hostBuilder,
        MessagingTransportType transport,
        Action<WolverineConfiguration>? configure = null
    )
    {
        hostBuilder.UseWolverine(opts =>
        {
            switch (transport)
            {
                case MessagingTransportType.RabbitMq:
                    opts.UseRabbitMqUsingNamedConnection("rabbitmq")
                        .AutoProvision()
                        .AutoPurgeOnStartup();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported transport: {transport}");
            }

            configure?.Invoke(new WolverineConfiguration(opts));
        });

        return hostBuilder;
    }

    /// <summary>
    /// Registers Wolverine with RabbitMQ transport for WebApplicationBuilder.
    /// </summary>
    public static WebApplicationBuilder AddTransactionalWolverine(
        this WebApplicationBuilder builder,
        MessagingTransportType transport,
        Action<WolverineConfiguration>? configure = null
    )
    {
        builder.Host.AddTransactionalWolverine(transport, configure);
        return builder;
    }
}

/// <summary>
/// Wraps WolverineOptions to provide a cleaner configuration API.
/// </summary>
public class WolverineConfiguration(WolverineOptions options)
{
    public WolverineOptions Options { get; } = options;

    public void ConfigureWolverine(Action<WolverineOptions> configure)
    {
        configure(Options);
    }

    /// <summary>
    /// Auto-discovers message handlers and saga types in the given assembly.
    /// </summary>
    public void ScanHandlers(Assembly assembly)
    {
        Options.Discovery.IncludeAssembly(assembly);
    }
}
