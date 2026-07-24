namespace BuildingBlocks.Integration.Wolverine.Kafka;

public sealed class WolverineKafkaOptions
{
    public required string ConnectionName { get; init; }

    public string? ConnectionString { get; init; }
}
