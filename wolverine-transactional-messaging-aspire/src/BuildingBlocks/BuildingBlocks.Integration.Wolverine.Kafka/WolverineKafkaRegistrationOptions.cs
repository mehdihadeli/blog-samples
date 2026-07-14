using BuildingBlocks.Integration.Wolverine.Configuration;

namespace BuildingBlocks.Integration.Wolverine.Kafka;

public sealed class WolverineKafkaRegistrationOptions
{
    public required WolverineCommonOptions Common { get; init; }

    public required WolverineKafkaOptions Kafka { get; init; }
}
