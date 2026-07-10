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
    public static IHostBuilder AddWolverineMessaging(
        this IHostBuilder hostBuilder,
        WolverineIntegrationOptions integrationOptions,
        Action<WolverineOptions> configure
    )
    {
        hostBuilder.UseWolverine(options =>
        {
            options.PersistMessagesWithPostgresql(
                integrationOptions.DurableStorageConnectionString
            );
            if (integrationOptions.Bus.UseEntityFrameworkCoreTransactions)
            {
                options.UseEntityFrameworkCoreTransactions();
            }

            if (integrationOptions.Bus.UseDurableLocalQueues)
            {
                options.Policies.UseDurableLocalQueues();
            }

            if (integrationOptions.Bus.UseDurableInboxOnAllListeners)
            {
                options.Policies.UseDurableInboxOnAllListeners();
            }

            configure(options);

            if (integrationOptions.Bus.Retry is { MaximumAttempts: > 0 })
            {
                var immediateRetries = integrationOptions.Bus.Retry.MaximumAttempts - 1;
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

        hostBuilder.ConfigureServices(services =>
        {
            services.AddScoped<IExternalEventBus, WolverineExternalEventBus>();
            services.AddScoped<IBusDirectPublisher, WolverineDirectPublisher>();
            services.AddScoped<IMessagePersistenceService, WolverineMessagePersistenceService>();

            if (
                integrationOptions.Bus.ConfigureRabbitMqTopology
                && !string.IsNullOrWhiteSpace(integrationOptions.RabbitMqConnectionString)
            )
            {
                services.AddHostedService(
                    serviceProvider => new RabbitMqTopologyProvisioningHostedService(
                        integrationOptions.RabbitMqConnectionString!,
                        serviceProvider.GetRequiredService<
                            ILogger<RabbitMqTopologyProvisioningHostedService>
                        >()
                    )
                );
            }
        });

        return hostBuilder;
    }
}
