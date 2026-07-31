namespace BuildingBlocks.Integration.Wolverine.Configuration;

public sealed class WolverineBusOptions
{
    public bool UseDurableInboxOnAllListeners { get; set; }
    public string DurableStorageConnectionString { get; set; } = string.Empty;

    public bool UseDurableLocalQueues { get; set; } = true;

    public bool UseEntityFrameworkCoreTransactions { get; set; } = true;

    public bool UseNativeDeadLetterQueue { get; set; } = true;

    public string? DeadLetterQueueName { get; set; }

    /// <summary>
    /// When <c>true</c> (default), topology is auto-discovered by scanning
    /// provided assemblies for <c>IIntegrationEvent</c> types with snake_case
    /// naming — no per-service topology file needed. Applies to both RabbitMQ
    /// and Kafka transports. Requires passing <c>assemblies</c> to the
    /// registration call; if omitted, neither auto nor manual topology runs.
    /// </summary>
    public bool AutoConfigMessagesTopology { get; set; } = true;

    public WolverineRetryOptions Retry { get; set; } = new();

    public MessagingTransportType TransportType { get; set; } = MessagingTransportType.RabbitMq;
    public string ConnectionName { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
}
