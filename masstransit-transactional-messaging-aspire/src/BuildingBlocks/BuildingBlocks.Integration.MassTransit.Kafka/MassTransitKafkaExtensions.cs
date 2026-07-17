using BuildingBlocks.Integration.MassTransit.Extensions;
using BuildingBlocks.Integration.MassTransit.Options;
using Confluent.Kafka;
using MassTransit;
using MassTransit.KafkaIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Integration.MassTransit.Kafka;

public static class MassTransitKafkaExtensions
{
    public static IServiceCollection AddMassTransitKafka<TDbContext>(
        this IServiceCollection services,
        MassTransitOptions options,
        MassTransitKafkaRegistrationOptions? registrationOptions = null,
        IHostEnvironment? environment = null
    )
        where TDbContext : DbContext
    {
        registrationOptions ??= new MassTransitKafkaRegistrationOptions();

        services.AddMassTransitMessaging<TDbContext>(
            options,
            x =>
            {
                registrationOptions.ConfigureBus?.Invoke(x);
                x.AddRider(rider =>
                {
                    registrationOptions.ConfigureRider?.Invoke(rider);
                    rider.UsingKafka(
                        (context, kafka) =>
                        {
                            kafka.Host(
                                options.KafkaConnectionString
                                    ?? throw new InvalidOperationException(
                                        "Missing connection string 'kafka'."
                                    )
                            );
                            registrationOptions.ConfigureTransport?.Invoke(context, kafka);
                        }
                    );
                });
                x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
            },
            registrationOptions.ConfigureMediator,
            serviceCollection =>
            {
                registrationOptions.ConfigurePublisher?.Invoke(serviceCollection);
                registrationOptions.ConfigureServices?.Invoke(serviceCollection);
            },
            environment
        );

        return services;
    }

    public static IServiceCollection AddMassTransitKafka<TDbContext>(
        this IServiceCollection services,
        MassTransitOptions options,
        Action<MassTransitKafkaRegistrationOptions> configure,
        IHostEnvironment? environment = null
    )
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configure);

        var registrationOptions = new MassTransitKafkaRegistrationOptions();
        configure(registrationOptions);

        return services.AddMassTransitKafka<TDbContext>(options, registrationOptions, environment);
    }

    /// <summary>
    /// Configures a Kafka topic endpoint with centralized retry and consumer outbox policies.
    /// Call this from <see cref="MassTransitKafkaRegistrationOptions.ConfigureTransport"/>.
    /// </summary>
    public static void TopicEndpointWithPolicies<TDbContext, TMessage, TConsumer>(
        this IKafkaFactoryConfigurator kafka,
        IRegistrationContext context,
        MassTransitBusOptions busOptions,
        string topicName,
        string groupId,
        Action<IKafkaTopicReceiveEndpointConfigurator<Ignore, TMessage>>? configure = null
    )
        where TDbContext : DbContext
        where TMessage : class
        where TConsumer : class, IConsumer<TMessage>
    {
        kafka.TopicEndpoint<TMessage>(
            topicName,
            groupId,
            endpoint =>
            {
                endpoint.ApplyEndpointPolicies<TDbContext>(context, busOptions);
                endpoint.ConfigureConsumer<TConsumer>(context);
                configure?.Invoke(endpoint);
            }
        );
    }
}
