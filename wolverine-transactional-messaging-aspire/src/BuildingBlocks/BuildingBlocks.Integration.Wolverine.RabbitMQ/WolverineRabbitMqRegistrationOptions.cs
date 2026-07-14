using BuildingBlocks.Integration.Wolverine.Configuration;

namespace BuildingBlocks.Integration.Wolverine.RabbitMQ;

public sealed class WolverineRabbitMqRegistrationOptions
{
    public required WolverineCommonOptions Common { get; init; }

    public required WolverineRabbitMqOptions RabbitMq { get; init; }
}
