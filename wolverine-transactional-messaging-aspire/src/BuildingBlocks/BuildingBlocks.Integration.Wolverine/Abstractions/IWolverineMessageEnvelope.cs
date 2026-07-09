namespace BuildingBlocks.Integration.Wolverine.Abstractions;

public interface IWolverineMessageEnvelope
{
    Guid MessageId { get; }

    Guid CorrelationId { get; }

    DateTime OccurredAtUtc { get; }
}