namespace BuildingBlocks.Integration.Wolverine.Abstractions;

public interface IEventBus
{
    Task PublishAsync<T>(T message, CancellationToken ct = default)
        where T : class;
}
