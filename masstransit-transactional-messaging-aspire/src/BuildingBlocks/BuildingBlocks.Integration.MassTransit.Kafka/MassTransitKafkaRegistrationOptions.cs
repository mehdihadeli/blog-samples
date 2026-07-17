using BuildingBlocks.Integration.MassTransit.Abstractions;
using BuildingBlocks.Integration.MassTransit.Options;
using MassTransit;
using MassTransit.KafkaIntegration;
using MassTransit.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Integration.MassTransit.Kafka;

public sealed class MassTransitKafkaRegistrationOptions
{
    public Action<IBusRegistrationConfigurator>? ConfigureBus { get; init; }

    public Action<IMediatorRegistrationConfigurator>? ConfigureMediator { get; init; }

    public Action<IRiderRegistrationConfigurator>? ConfigureRider { get; init; }

    public Action<
        IRiderRegistrationContext,
        IKafkaFactoryConfigurator
    >? ConfigureTransport { get; init; }

    public Action<IServiceCollection>? ConfigureServices { get; init; }

    public Action<IServiceCollection>? ConfigurePublisher { get; init; }
}

/// <summary>
/// Fluent builder for registering Kafka publishers and listeners in a transport-agnostic style,
/// similar to Wolverine's <c>WolverineKafkaRegistrationBuilder</c>.
/// </summary>
public sealed class MassTransitKafkaRegistrationBuilder
{
    private readonly MassTransitOptions _options;
    private Action<IBusRegistrationConfigurator>? _configureBus;
    private Action<IMediatorRegistrationConfigurator>? _configureMediator;
    private Action<IRiderRegistrationConfigurator>? _configureRider;
    private Action<IRiderRegistrationContext, IKafkaFactoryConfigurator>? _configureTransport;
    private Action<IServiceCollection>? _configureServices;
    private Action<IServiceCollection>? _configurePublisher;

    internal MassTransitKafkaRegistrationBuilder(MassTransitOptions options)
    {
        _options = options;
    }

    public MassTransitKafkaRegistrationBuilder ConfigureBus(
        Action<IBusRegistrationConfigurator> configure
    )
    {
        _configureBus = configure;
        return this;
    }

    public MassTransitKafkaRegistrationBuilder ConfigureMediator(
        Action<IMediatorRegistrationConfigurator> configure
    )
    {
        _configureMediator = configure;
        return this;
    }

    public MassTransitKafkaRegistrationBuilder ConfigureRider(
        Action<IRiderRegistrationConfigurator> configure
    )
    {
        _configureRider = configure;
        return this;
    }

    public MassTransitKafkaRegistrationBuilder ConfigureTransport(
        Action<IRiderRegistrationContext, IKafkaFactoryConfigurator> configure
    )
    {
        _configureTransport = configure;
        return this;
    }

    public MassTransitKafkaRegistrationBuilder ConfigureServices(
        Action<IServiceCollection> configure
    )
    {
        _configureServices = configure;
        return this;
    }

    public MassTransitKafkaRegistrationBuilder ConfigurePublisher(
        Action<IServiceCollection> configure
    )
    {
        _configurePublisher = configure;
        return this;
    }

    internal MassTransitKafkaRegistrationOptions Build() =>
        new()
        {
            ConfigureBus = _configureBus,
            ConfigureMediator = _configureMediator,
            ConfigureRider = _configureRider,
            ConfigureTransport = _configureTransport,
            ConfigureServices = _configureServices,
            ConfigurePublisher = _configurePublisher,
        };
}

public static class MassTransitKafkaPublisherRegistrationExtensions
{
    public static IServiceCollection AddKafkaMessagePublisher<TMessage>(
        this IServiceCollection services
    )
        where TMessage : class
    {
        services.AddScoped<IMassTransitMessagePublisher, KafkaTopicMessagePublisher<TMessage>>();
        return services;
    }
}

internal sealed class KafkaTopicMessagePublisher<TMessage>(ITopicProducer<TMessage> producer)
    : IMassTransitMessagePublisher
    where TMessage : class
{
    public Task PublishAsync<TRequestedMessage>(
        TRequestedMessage message,
        CancellationToken cancellationToken
    )
        where TRequestedMessage : class
    {
        if (message is not TMessage typedMessage)
        {
            throw new InvalidOperationException(
                $"Kafka publisher does not support message type '{typeof(TRequestedMessage).Name}'. Expected '{typeof(TMessage).Name}'."
            );
        }

        return producer.Produce(typedMessage, cancellationToken);
    }
}
