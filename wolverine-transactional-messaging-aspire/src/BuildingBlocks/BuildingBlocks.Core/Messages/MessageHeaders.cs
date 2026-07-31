namespace BuildingBlocks.Core.Messages;

/// <summary>
///     Well-known message header keys propagated over RabbitMQ / Kafka
///     via Wolverine <c>DeliveryOptions.WithHeader()</c>.
///     Supported levels: core/utility — used by Levels 1-4 for cross-broker metadata propagation.
/// </summary>
public static class MessageHeaders
{
    public const string MessageId = "message-id";
    public const string CorrelationId = "correlation-id";
    public const string CausationId = "causation-id";
    public const string TraceId = "trace-id";
    public const string SpanId = "span-id";
    public const string ParentSpanId = "parent-id";
    public const string Name = "name";
    public const string Type = "type";
    public const string Created = "created";
    public const string OccurredAtUtc = "occurred-at-utc";
    public const string ExchangeOrTopic = "exchange-topic";
    public const string Queue = "queue";
}
