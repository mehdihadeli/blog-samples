namespace BuildingBlocks.Abstractions.Messages;

public interface IMessage;
public interface IIntegrationEvent : IMessage;
public interface IInternalCommand : IMessage;

public interface IMessageEnvelopeMetadata
{
    Guid MessageId { get; }
    Guid CorrelationId { get; }
    DateTime OccurredAtUtc { get; }
    Type MessageType { get; }
}
