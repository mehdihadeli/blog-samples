using System.Reflection;
using BuildingBlocks.Core.Extensions;
using BuildingBlocks.Core.Messages;
using BuildingBlocks.Integration.Wolverine.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;
using WolverineOptions = Wolverine.WolverineOptions;

namespace BuildingBlocks.Integration.Wolverine.Extensions;

public static class Extensions
{
    public static IHostApplicationBuilder AddWolverineMessaging(
        this IHostApplicationBuilder builder,
        Action<WolverineBusOptions>? wolverineBusOptionsConfigure = null,
        Action<WolverineOptions, WolverineBusOptions>? configure = null,
        IReadOnlyCollection<Assembly>? assemblies = null
    )
    {
        var wolverineBusOptions = builder.Configuration.BindOptions<WolverineBusOptions>(
            nameof(WolverineBusOptions)
        );
        wolverineBusOptionsConfigure?.Invoke(wolverineBusOptions);

        builder.Services.AddWolverine(options =>
        {
            // Persistence is optional: when no durable-storage connection string is
            // configured (e.g. isolated building-block tests), Wolverine falls back to
            // its in-memory message store so no Postgres polling agents are started.
            if (!string.IsNullOrWhiteSpace(wolverineBusOptions.DurableStorageConnectionString))
            {
                options.PersistMessagesWithPostgresql(
                    connectionString: wolverineBusOptions.DurableStorageConnectionString,
                    schemaName: null
                );
            }

            if (wolverineBusOptions.UseEntityFrameworkCoreTransactions)
            {
                options.UseEntityFrameworkCoreTransactions();
            }

            if (wolverineBusOptions.UseDurableLocalQueues)
            {
                options.Policies.UseDurableLocalQueues();
            }

            if (wolverineBusOptions.UseDurableInboxOnAllListeners)
            {
                options.Policies.UseDurableInboxOnAllListeners();
            }

            foreach (var assembly in assemblies ?? Array.Empty<Assembly>())
            {
                // Explicitly register each handler assembly for Wolverine
                // discovery — equivalent to decorating the assembly with
                // [assembly: WolverineModule].
                options.Discovery.IncludeAssembly(assembly);
            }

            configure?.Invoke(options, wolverineBusOptions);

            if (wolverineBusOptions.Retry is { MaximumAttempts: > 0 })
            {
                var immediateRetries = wolverineBusOptions.Retry.MaximumAttempts - 1;
                if (immediateRetries > 0)
                {
                    options
                        .OnException<Exception>()
                        .RetryTimes(immediateRetries)
                        .Then.MoveToErrorQueue();
                }
                else
                {
                    options.OnException<Exception>().MoveToErrorQueue();
                }
            }
        });

        builder.Services.AddScoped<IMessageMetadataAccessor, MessageMetadataAccessor>();
        builder.Services.AddScoped<IExternalEventBus, WolverineExternalEventBus>();
        builder.Services.AddScoped<IBusDirectPublisher, WolverineDirectPublisher>();
        builder.Services.AddScoped<
            IMessagePersistenceService,
            WolverineMessagePersistenceService
        >();
        builder.Services.AddScoped<IBackgroundJobScheduler, WolverineBackgroundJobScheduler>();

        return builder;
    }
}
