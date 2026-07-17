namespace BuildingBlocks.Integration.MassTransit.Options;

public sealed class MassTransitOptions
{
    public required string DurableStorageConnectionString { get; init; }
    public string? RabbitMqConnectionString { get; init; }
    public string? KafkaConnectionString { get; init; }
    public MassTransitBusOptions Bus { get; init; } = new();
}
