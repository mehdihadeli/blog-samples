namespace BuildingBlocks.Core.Messages;

/// <summary>
///     Metadata record carried inside every envelope.
///     Holds identifiers, correlation/causation chain, type names, timestamps, and custom headers.
///     Propagated over the broker via <c>DeliveryOptions</c> headers.
///     Supported levels: core/utility — shared across Levels 1-5 for tracing, deduplication, and delivery configuration.
/// </summary>
public record MessageEnvelopeMetadata(
    Guid MessageId,
    Guid CorrelationId,
    string MessageType,
    string Name,
    Guid? CausationId
)
{
    public IDictionary<string, object?> Headers { get; init; } = new Dictionary<string, object?>();

    public DateTime Created { get; init; } = DateTime.UtcNow;

    public long? CreatedUnixTime { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
