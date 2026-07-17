namespace BuildingBlocks.Integration.MassTransit.Options;

public sealed class MassTransitBusOptions
{
    public bool UseConsumerOutbox { get; set; } = true;
    public bool UseBusOutbox { get; set; } = true;
    public bool UsePostCommitMediator { get; set; } = true;
    public bool UseErrorTransport { get; set; } = true;
    public string? ErrorQueueName { get; set; }
    public bool UseEnvelopePublisher { get; set; } = true;
    public MassTransitRetryOptions Retry { get; set; } = new();
}
