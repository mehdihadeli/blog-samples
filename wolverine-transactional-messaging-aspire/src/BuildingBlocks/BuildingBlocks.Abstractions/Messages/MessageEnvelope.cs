namespace BuildingBlocks.Abstractions.Messages;

/// <summary>
///     Generic typed envelope wrapping a message with its metadata.
///     Implements the <see cref="IMessageEnvelope" /> pattern (EIP Envelope Wrapper).
///     Supported levels: core/utility — used by all levels (1-5) for transport, persistence, and handling.
/// </summary>
public record MessageEnvelope<T>(T Message, MessageEnvelopeMetadata Metadata) : IMessageEnvelope
    where T : class, IMessage
{
    object IMessageEnvelope.Message => Message;
}
