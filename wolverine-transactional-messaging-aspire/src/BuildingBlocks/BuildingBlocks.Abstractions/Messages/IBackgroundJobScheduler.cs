namespace BuildingBlocks.Abstractions.Messages;

/// <summary>
///     Schedules messages for delayed (future) execution via Wolverine's <c>IMessageScheduler</c>.
///     Entries are stored in the <c>wolverine_scheduled_messages</c> PostgreSQL table — NOT the broker,
///     NOT the outbox/local-queue table — so they survive process restarts.
///     A background polling agent (default interval: 10s) picks due messages and invokes handlers locally.
///     Supported level: 4 (Background Job Scheduler) — PostgreSQL-backed, broker-independent.
/// </summary>
public interface IBackgroundJobScheduler
{
    ValueTask ScheduleAsync<T>(
        T message,
        DateTimeOffset scheduledTime,
        CancellationToken ct = default
    )
        where T : class, IMessage;

    ValueTask ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct = default)
        where T : class, IMessage;
}
