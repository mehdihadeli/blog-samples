using BuildingBlocks.Integration.MassTransit.Abstractions;
using BuildingBlocks.Integration.MassTransit.Options;
using MassTransit;
using MassTransit.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Integration.MassTransit.Extensions;

public static class MassTransitServiceCollectionExtensions
{
    public static IServiceCollection AddMassTransitMessaging<TDbContext>(
        this IServiceCollection services,
        MassTransitOptions options,
        Action<IBusRegistrationConfigurator> configure,
        Action<IMediatorRegistrationConfigurator>? configureMediator = null,
        Action<IServiceCollection>? configureServices = null,
        IHostEnvironment? environment = null
    )
        where TDbContext : DbContext
    {
        if (options.Bus.UsePostCommitMediator)
        {
            services.AddMediator(x =>
            {
                configureMediator?.Invoke(x);
            });
        }

        if (environment?.IsEnvironment("Test") == true)
        {
            services.AddMassTransitTestHarness(cfg =>
            {
                if (options.Bus.UseBusOutbox)
                {
                    cfg.AddEntityFrameworkOutbox<TDbContext>(o =>
                    {
                        o.UsePostgres();
                        o.UseBusOutbox();
                    });
                }

                configure(cfg);
            });
        }
        else
        {
            services.AddMassTransit(x =>
            {
                if (options.Bus.UseBusOutbox)
                {
                    x.AddEntityFrameworkOutbox<TDbContext>(o =>
                    {
                        o.UsePostgres();
                        o.UseBusOutbox();
                    });
                }

                configure(x);
            });
        }

        if (options.Bus.UseEnvelopePublisher)
        {
            services.AddScoped<IMassTransitMessagePublisher, MassTransitEnvelopePublisher>();
        }
        else
        {
            services.AddScoped<IMassTransitMessagePublisher, MassTransitPublishEndpointPublisher>();
        }

        services.AddScoped<IEventBus, MassTransitEventBus>();
        services.AddScoped<IInternalCommandBus, MassTransitInternalCommandBus>();
        configureServices?.Invoke(services);

        return services;
    }

    /// <summary>
    /// Applies centralized retry, delayed redelivery, consumer outbox, and error transport
    /// to a receive endpoint configurator based on the bus options. Call this from
    /// RabbitMQ or Kafka transport configuration callbacks.
    /// </summary>
    public static void ApplyEndpointPolicies<TDbContext>(
        this IReceiveEndpointConfigurator endpoint,
        IRegistrationContext context,
        MassTransitBusOptions busOptions
    )
        where TDbContext : DbContext
    {
        // Retry — immediate retries first, then delayed redelivery
        var immediateRetries = busOptions.Retry.MaximumAttempts - 1;
        if (immediateRetries > 0)
        {
            endpoint.UseMessageRetry(r => r.Immediate(immediateRetries));
        }

        if (busOptions.Retry.UseDelayedRedelivery)
        {
            endpoint.UseDelayedRedelivery(r =>
                r.Intervals(busOptions.Retry.DelayedRedeliveryIntervals)
            );
        }

        // Consumer outbox — provides idempotent consumption
        if (busOptions.UseConsumerOutbox)
        {
            endpoint.UseEntityFrameworkOutbox<TDbContext>(context);
        }

        // Error transport — discard faulted when error transport disabled
        if (!busOptions.UseErrorTransport)
        {
            endpoint.DiscardFaultedMessages();
        }
    }
}

public sealed class MassTransitPublishEndpointPublisher(IPublishEndpoint publishEndpoint)
    : IMassTransitMessagePublisher
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class => publishEndpoint.Publish(message, cancellationToken);
}
