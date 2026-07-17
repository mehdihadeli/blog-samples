using BuildingBlocks.Integration.MassTransit.Extensions;
using BuildingBlocks.Integration.MassTransit.Options;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Integration.MassTransit.RabbitMQ;

public static class MassTransitRabbitMqExtensions
{
    public static IServiceCollection AddMassTransitRabbitMq<TDbContext>(
        this IServiceCollection services,
        MassTransitOptions options,
        MassTransitRabbitMqRegistrationOptions? registrationOptions = null,
        IHostEnvironment? environment = null
    )
        where TDbContext : DbContext
    {
        registrationOptions ??= new MassTransitRabbitMqRegistrationOptions();

        services.AddMassTransitMessaging<TDbContext>(
            options,
            x =>
            {
                registrationOptions.ConfigureBus?.Invoke(x);
                x.UsingRabbitMq(
                    (context, cfg) =>
                    {
                        cfg.Host(
                            new Uri(
                                options.RabbitMqConnectionString
                                    ?? throw new InvalidOperationException(
                                        "Missing connection string 'rabbitmq'."
                                    )
                            )
                        );
                        registrationOptions.ConfigureTransport?.Invoke(context, cfg);
                        cfg.ConfigureEndpoints(context);
                    }
                );
            },
            registrationOptions.ConfigureMediator,
            serviceCollection =>
            {
                registrationOptions.ConfigureServices?.Invoke(serviceCollection);
            },
            environment
        );

        return services;
    }

    public static IServiceCollection AddMassTransitRabbitMq<TDbContext>(
        this IServiceCollection services,
        MassTransitOptions options,
        Action<MassTransitRabbitMqRegistrationOptions> configure,
        IHostEnvironment? environment = null
    )
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configure);

        var registrationOptions = new MassTransitRabbitMqRegistrationOptions();
        configure(registrationOptions);

        return services.AddMassTransitRabbitMq<TDbContext>(
            options,
            registrationOptions,
            environment
        );
    }

    /// <summary>
    /// Sets the entity name for a publish message type. MassTransit auto-creates
    /// the exchange, queue, and binding when <c>ConfigureEndpoints</c> runs.
    /// </summary>
    public static void PublishToRabbitQueue<T>(
        this IRabbitMqBusFactoryConfigurator cfg,
        string exchangeName
    )
        where T : class
    {
        cfg.Message<T>(m => m.SetEntityName(exchangeName));
    }

    /// <summary>
    /// Configures a receive endpoint with centralized retry, delayed redelivery,
    /// and consumer outbox policies, then wires up the consumer.
    /// </summary>
    public static void ReceiveEndpointWithPolicies<TDbContext, TConsumer>(
        this IRabbitMqBusFactoryConfigurator cfg,
        IRegistrationContext context,
        MassTransitBusOptions busOptions,
        string queueName,
        Action<IRabbitMqReceiveEndpointConfigurator>? configureEndpoint = null
    )
        where TDbContext : DbContext
        where TConsumer : class, IConsumer
    {
        cfg.ReceiveEndpoint(
            queueName,
            endpoint =>
            {
                endpoint.ApplyEndpointPolicies<TDbContext>(context, busOptions);
                endpoint.ConfigureConsumer<TConsumer>(context);
                configureEndpoint?.Invoke(endpoint);
            }
        );
    }
}
