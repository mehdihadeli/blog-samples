namespace BuildingBlocks.Integration.Wolverine.Configuration;

public sealed class WolverineIntegrationOptions
{
    public required string DurableStorageConnectionString { get; init; }

    public string? RabbitMqConnectionString { get; init; }

    public WolverineBusOptions Bus { get; init; } = new();
}
