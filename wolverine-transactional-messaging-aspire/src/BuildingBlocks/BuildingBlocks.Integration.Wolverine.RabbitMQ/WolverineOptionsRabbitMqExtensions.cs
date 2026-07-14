using BuildingBlocks.Integration.Wolverine;
using BuildingBlocks.Integration.Wolverine.Configuration;
using BuildingBlocks.Integration.Wolverine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.RabbitMQ;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ;

public static class WolverineOptionsRabbitMqExtensions
{
    public static IHostApplicationBuilder AddWolverineRabbitMq(
        this IHostApplicationBuilder builder,
        WolverineRabbitMqRegistrationOptions registrationOptions,
        Action<WolverineRabbitMqRegistrationBuilder>? configure = null
    )
    {
        builder.AddWolverineMessaging(
            registrationOptions.Common,
            options =>
                options.UseRabbitMqTransport(
                    registrationOptions.RabbitMq.ConnectionName,
                    rabbitMqOptions =>
                    {
                        var registrationBuilder = new WolverineRabbitMqRegistrationBuilder(
                            rabbitMqOptions,
                            registrationOptions.Common.Bus
                        );

                        configure?.Invoke(registrationBuilder);
                    },
                    registrationOptions.Common.Bus
                )
        );

        if (
            registrationOptions.RabbitMq.ConfigureTopology
            && !string.IsNullOrWhiteSpace(registrationOptions.RabbitMq.ConnectionString)
        )
        {
            builder.Services.AddHostedService(
                serviceProvider => new RabbitMqTopologyProvisioningHostedService(
                    registrationOptions.RabbitMq.ConnectionString!,
                    serviceProvider.GetRequiredService<
                        ILogger<RabbitMqTopologyProvisioningHostedService>
                    >()
                )
            );
        }

        return builder;
    }

    public static WolverineOptions UseRabbitMqTransport(
        this WolverineOptions options,
        string connectionName,
        Action<WolverineOptions> configure,
        WolverineBusOptions? busOptions = null
    )
    {
        var transport = options.UseRabbitMqUsingNamedConnection(connectionName).AutoProvision();

        if (!string.IsNullOrWhiteSpace(busOptions?.DeadLetterQueueName))
        {
            transport.CustomizeDeadLetterQueueing(
                new DeadLetterQueue(busOptions.DeadLetterQueueName)
            );
        }

        configure(options);

        return options;
    }

    public static WolverineOptions PublishToRabbitQueue<T>(
        this WolverineOptions options,
        string queueName
    )
    {
        WolverineMessageTopologyExtensions.RegisterPublishTopology<T>(queueName, queueName);
        options.PublishMessage<T>().ToRabbitQueue(queueName);

        return options;
    }

    public static RabbitMqListenerConfiguration ListenToRabbitQueueTransport(
        this WolverineOptions options,
        string queueName,
        WolverineBusOptions? busOptions = null
    )
    {
        WolverineMessageTopologyExtensions.RegisterListenerTopology(
            queueName,
            queueName,
            queueName
        );

        var listener = options.ListenToRabbitQueue(queueName);

        if (busOptions?.UseNativeDeadLetterQueue == false)
        {
            listener.DisableDeadLetterQueueing();
        }
        else if (!string.IsNullOrWhiteSpace(busOptions?.DeadLetterQueueName))
        {
            listener.DeadLetterQueueing(new DeadLetterQueue(busOptions.DeadLetterQueueName));
        }

        return listener;
    }
}

public sealed class WolverineRabbitMqRegistrationBuilder
{
    private readonly WolverineOptions _options;
    private readonly WolverineBusOptions? _busOptions;

    internal WolverineRabbitMqRegistrationBuilder(
        WolverineOptions options,
        WolverineBusOptions? busOptions
    )
    {
        _options = options;
        _busOptions = busOptions;
    }

    public WolverineRabbitMqRegistrationBuilder Publish<T>(string queueName)
    {
        _options.PublishToRabbitQueue<T>(queueName);

        return this;
    }

    public WolverineRabbitMqRegistrationBuilder Listen<T>(
        string queueName,
        Action<RabbitMqListenerConfiguration>? configure = null
    )
    {
        var listener = _options.ListenToRabbitQueueTransport(queueName, _busOptions);
        listener.DefaultIncomingMessage<T>();
        configure?.Invoke(listener);

        return this;
    }
}
