namespace BuildingBlocks.Abstractions.Messages;

public interface IMessage;

public interface IIntegrationEvent : IMessage;

public interface IInternalCommand : IMessage;
