using BuildingBlocks.Integration.MassTransit.Options;
using MassTransit;
using MassTransit.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Integration.MassTransit.RabbitMQ;

/// <summary>
/// Defines RabbitMQ-specific extension points for a microservice using the shared MassTransit building blocks.
/// </summary>
/// <remarks>
/// Use this options object to keep shared transport bootstrapping in building blocks while allowing each
/// microservice to register its own consumers, endpoints, and extra service registrations.
/// </remarks>
public sealed class MassTransitRabbitMqRegistrationOptions
{
    /// <summary>
    /// Registers consumers, sagas, or other bus-level MassTransit components before transport configuration.
    /// </summary>
    public Action<IBusRegistrationConfigurator>? ConfigureBus { get; init; }

    public Action<IMediatorRegistrationConfigurator>? ConfigureMediator { get; init; }

    /// <summary>
    /// Configures RabbitMQ-specific topology such as receive endpoints, entity names, bindings, and queue arguments.
    /// </summary>
    public Action<
        IBusRegistrationContext,
        IRabbitMqBusFactoryConfigurator
    >? ConfigureTransport { get; init; }

    /// <summary>
    /// Adds extra service registrations needed only by the current microservice.
    /// </summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }
}

/// <summary>
/// Fluent builder for registering publishes and listeners in a transport-agnostic style,
/// similar to Wolverine's <c>WolverineRabbitMqRegistrationBuilder</c>.
/// </summary>
public sealed class MassTransitRabbitMqRegistrationBuilder
{
    private readonly MassTransitOptions _options;
    private Action<IBusRegistrationConfigurator>? _configureBus;
    private Action<IMediatorRegistrationConfigurator>? _configureMediator;
    private Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? _configureTransport;
    private Action<IServiceCollection>? _configureServices;

    internal MassTransitRabbitMqRegistrationBuilder(MassTransitOptions options)
    {
        _options = options;
    }

    public MassTransitRabbitMqRegistrationBuilder ConfigureBus(
        Action<IBusRegistrationConfigurator> configure
    )
    {
        _configureBus = configure;
        return this;
    }

    public MassTransitRabbitMqRegistrationBuilder ConfigureMediator(
        Action<IMediatorRegistrationConfigurator> configure
    )
    {
        _configureMediator = configure;
        return this;
    }

    public MassTransitRabbitMqRegistrationBuilder ConfigureTransport(
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator> configure
    )
    {
        _configureTransport = configure;
        return this;
    }

    public MassTransitRabbitMqRegistrationBuilder ConfigureServices(
        Action<IServiceCollection> configure
    )
    {
        _configureServices = configure;
        return this;
    }

    internal MassTransitRabbitMqRegistrationOptions Build() =>
        new()
        {
            ConfigureBus = _configureBus,
            ConfigureMediator = _configureMediator,
            ConfigureTransport = _configureTransport,
            ConfigureServices = _configureServices,
        };
}
