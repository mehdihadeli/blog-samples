using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.MassTransit.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.DurableLocalQueue;

/// <summary>
/// Extension methods for registering the durable local queue infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the durable internal command bus and background processor,
    /// replacing the default mediator-based <see cref="IInternalCommandBus"/>.
    /// </summary>
    /// <typeparam name="TDbContext">The EF Core DbContext that holds the DurableMessage table.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration for processor options.</param>
    public static IServiceCollection AddDurableLocalQueue<TDbContext>(
        this IServiceCollection services,
        Action<DurableCommandProcessorOptions>? configure = null
    )
        where TDbContext : DbContext
    {
        var options = new DurableCommandProcessorOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // Register the durable bus scoped (writes within the HTTP request transaction)
        services.Replace(
            ServiceDescriptor.Scoped<IInternalCommandBus, DurableInternalCommandBus<TDbContext>>()
        );

        // Register the background processor as a singleton hosted service
        services.AddSingleton<DurableCommandProcessor<TDbContext>>();
        services.AddHostedService(sp =>
            sp.GetRequiredService<DurableCommandProcessor<TDbContext>>()
        );

        return services;
    }

    /// <summary>
    /// Registers a handler for a specific command type. The handler receives
    /// the deserialized command, an IServiceProvider for resolving dependencies,
    /// and a CancellationToken. It is invoked by <see cref="DurableCommandProcessor{TDbContext}"/>
    /// when a matching command is polled from the outbox table.
    /// </summary>
    public static IServiceCollection AddDurableCommandHandler<T>(
        this IServiceCollection services,
        Func<T, IServiceProvider, CancellationToken, Task> handler
    )
        where T : class, IInternalCommand
    {
        DurableCommandHandlerRegistry.Register(handler);
        return services;
    }
}
