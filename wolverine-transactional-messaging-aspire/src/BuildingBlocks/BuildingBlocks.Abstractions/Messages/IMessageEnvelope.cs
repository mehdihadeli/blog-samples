namespace BuildingBlocks.Abstractions.Messages;

/// <summary>
///     The Envelope Wrapper Pattern standardizes message handling by
///     wrapping messages with metadata (IDs, timestamps, correlation, causation).
///     See: https://www.enterpriseintegrationpatterns.com/patterns/messaging/EnvelopeWrapper.html
///     Supported levels: core/utility — shared infrastructure for Levels 1-5.
/// </summary>
public interface IMessageEnvelope
{
    object Message { get; }
    MessageEnvelopeMetadata Metadata { get; }
}
