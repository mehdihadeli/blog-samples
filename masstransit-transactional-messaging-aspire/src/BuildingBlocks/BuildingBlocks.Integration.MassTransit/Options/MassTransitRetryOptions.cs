namespace BuildingBlocks.Integration.MassTransit.Options;

public sealed class MassTransitRetryOptions
{
    public int MaximumAttempts { get; set; } = 3;

    public bool UseDelayedRedelivery { get; set; } = true;

    public TimeSpan[] DelayedRedeliveryIntervals { get; set; } =
        [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)];
}
