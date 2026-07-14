namespace BuildingBlocks.Integration.Wolverine.Configuration;

public sealed class WolverineRabbitMqOptions
{
    public required string ConnectionName { get; init; }

    public string? ConnectionString { get; init; }

    public bool ConfigureTopology { get; init; }
}
