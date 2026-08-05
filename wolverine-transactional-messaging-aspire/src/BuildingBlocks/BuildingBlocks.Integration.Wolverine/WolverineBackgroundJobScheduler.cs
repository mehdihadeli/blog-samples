using BuildingBlocks.Core.Messages;
using Wolverine;

namespace BuildingBlocks.Integration.Wolverine;

internal sealed class WolverineBackgroundJobScheduler(IMessageBus bus) : IBackgroundJobScheduler
{
    ValueTask IBackgroundJobScheduler.ScheduleAsync<T>(
        T message,
        DateTimeOffset scheduledTime,
        CancellationToken ct
    )
    {
        return bus.ScheduleAsync(message, scheduledTime);
    }

    ValueTask IBackgroundJobScheduler.ScheduleAsync<T>(
        T message,
        TimeSpan delay,
        CancellationToken ct
    )
    {
        return bus.ScheduleAsync(message, delay);
    }
}
