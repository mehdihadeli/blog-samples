namespace BuildingBlocks.Integration.Wolverine.Abstractions;

public interface IInternalCommandBus
{
    Task SendAsync<T>(T command, CancellationToken ct = default)
        where T : class;
    Task ScheduleAsync<T>(T command, TimeSpan delay, CancellationToken ct = default)
        where T : class;
}
