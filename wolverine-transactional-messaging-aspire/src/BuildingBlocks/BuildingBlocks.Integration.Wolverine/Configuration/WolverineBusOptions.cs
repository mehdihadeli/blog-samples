namespace BuildingBlocks.Integration.Wolverine.Configuration;

public sealed class WolverineBusOptions
{
    public bool ConfigureRabbitMqTopology { get; set; }

    public bool UseDurableInboxOnAllListeners { get; set; }

    public bool UseDurableLocalQueues { get; set; } = true;

    public bool UseEntityFrameworkCoreTransactions { get; set; } = true;
}
