namespace BuildingBlocks.DurableLocalQueue;

/// <summary>
/// Configuration options for the durable command processor.
/// </summary>
public sealed class DurableCommandProcessorOptions
{
    /// <summary>Interval between polling cycles. Default: 2 seconds.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum number of commands to fetch per polling cycle. Default: 10.</summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>Maximum number of retry attempts before marking as Failed. Default: 3.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Time after which a command stuck in Processing status is considered stale
    /// and will be re-claimed. Default: 5 minutes.
    /// </summary>
    public TimeSpan StaleProcessingThreshold { get; set; } = TimeSpan.FromMinutes(5);
}
