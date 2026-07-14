using BuildingBlocks.Integration.Wolverine.Abstractions;
using BuildingBlocks.Integration.Wolverine.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

namespace BuildingBlocks.Integration.Wolverine.Extensions;

public static class WolverineHostBuilderExtensions
{
    public static IHostApplicationBuilder AddWolverineMessaging(
        this IHostApplicationBuilder builder,
        WolverineCommonOptions commonOptions,
        Action<WolverineOptions>? configure = null
    )
    {
        builder.Services.AddWolverine(options =>
        {
            options.PersistMessagesWithPostgresql(commonOptions.DurableStorageConnectionString);
            if (commonOptions.Bus.UseEntityFrameworkCoreTransactions)
            {
                options.UseEntityFrameworkCoreTransactions();
            }

            if (commonOptions.Bus.UseDurableLocalQueues)
            {
                options.Policies.UseDurableLocalQueues();
            }

            if (commonOptions.Bus.UseDurableInboxOnAllListeners)
            {
                options.Policies.UseDurableInboxOnAllListeners();
            }

            foreach (var assembly in commonOptions.HandlerAssemblies)
            {
                options.Discovery.IncludeAssembly(assembly);
            }

            configure?.Invoke(options);

            if (commonOptions.Bus.Retry is { MaximumAttempts: > 0 })
            {
                var immediateRetries = commonOptions.Bus.Retry.MaximumAttempts - 1;
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

        builder.Services.AddScoped<IExternalEventBus, WolverineExternalEventBus>();
        builder.Services.AddScoped<IBusDirectPublisher, WolverineDirectPublisher>();
        builder.Services.AddScoped<
            IMessagePersistenceService,
            WolverineMessagePersistenceService
        >();

        return builder;
    }
}
