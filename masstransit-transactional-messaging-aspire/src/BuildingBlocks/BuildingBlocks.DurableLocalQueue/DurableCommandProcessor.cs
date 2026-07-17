using BuildingBlocks.Abstractions.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BuildingBlocks.DurableLocalQueue;

/// <summary>
/// Background service that polls the durable command table, deserializes pending commands,
/// and dispatches them to registered handlers. Implements the Wolverine-style
/// "durability agent" pattern: survive restarts, retry on failure, mark completed.
/// </summary>
internal sealed class DurableCommandProcessor<TDbContext>(
    IServiceScopeFactory scopeFactory,
    DurableCommandProcessorOptions options,
    ILogger<DurableCommandProcessor<TDbContext>> logger
) : BackgroundService
    where TDbContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "DurableCommandProcessor started. Polling every {Interval}ms, batch size {Batch}",
            options.PollingInterval.TotalMilliseconds,
            options.BatchSize
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingCommandsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "DurableCommandProcessor polling cycle failed");
            }

            await Task.Delay(options.PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingCommandsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // 1. Reclaim stale Processing commands (node crash recovery)
        await ReclaimStaleCommandsAsync(dbContext, cancellationToken);

        // 2. Fetch pending commands
        var commands = await dbContext
            .Set<DurableMessage>()
            .Where(c => c.Status == DurableMessageStatus.Pending)
            .OrderBy(c => c.EnqueuedAtUtc)
            .Take(options.BatchSize)
            .ToListAsync(cancellationToken);

        if (commands.Count == 0)
            return;

        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Mark as Processing to prevent other nodes from picking it up
            command.Status = DurableMessageStatus.Processing;
            command.LastAttemptAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                await DispatchCommandAsync(command, scope.ServiceProvider, cancellationToken);

                command.Status = DurableMessageStatus.Completed;
                command.CompletedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogDebug("Command {Id} ({Type}) completed", command.Id, command.TypeName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                command.RetryCount++;
                command.LastError = ex.ToString();

                if (command.RetryCount >= options.MaxRetries)
                {
                    command.Status = DurableMessageStatus.Failed;
                    logger.LogError(
                        ex,
                        "Command {Id} ({Type}) failed after {Retries} retries — moved to Failed",
                        command.Id,
                        command.TypeName,
                        command.RetryCount
                    );
                }
                else
                {
                    command.Status = DurableMessageStatus.Pending;
                    logger.LogWarning(
                        ex,
                        "Command {Id} ({Type}) failed (attempt {Retries}/{Max}) — will retry",
                        command.Id,
                        command.TypeName,
                        command.RetryCount,
                        options.MaxRetries
                    );
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task ReclaimStaleCommandsAsync(TDbContext dbContext, CancellationToken ct)
    {
        var staleThreshold = DateTime.UtcNow - options.StaleProcessingThreshold;
        var stale = await dbContext
            .Set<DurableMessage>()
            .Where(c =>
                c.Status == DurableMessageStatus.Processing && c.LastAttemptAtUtc < staleThreshold
            )
            .ToListAsync(ct);

        foreach (var cmd in stale)
        {
            cmd.Status = DurableMessageStatus.Pending;
            logger.LogWarning(
                "Reclaimed stale command {Id} ({Type}) — last attempt at {LastAttempt}",
                cmd.Id,
                cmd.TypeName,
                cmd.LastAttemptAtUtc
            );
        }

        if (stale.Count > 0)
            await dbContext.SaveChangesAsync(ct);
    }

    private static async Task DispatchCommandAsync(
        DurableMessage entity,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        var type = Type.GetType(entity.TypeName);
        if (type is null)
        {
            throw new InvalidOperationException(
                $"Cannot resolve type '{entity.TypeName}' for command {entity.Id}"
            );
        }

        if (!DurableCommandHandlerRegistry.TryGet(type, out var handler))
        {
            throw new InvalidOperationException(
                $"No handler registered for command type '{entity.TypeName}'. "
                    + $"Use AddDurableCommandHandler<T>() to register one."
            );
        }

        var command = JsonConvert.DeserializeObject(
            entity.Payload,
            type,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }
        );

        if (command is null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize command {entity.Id} of type '{entity.TypeName}'"
            );
        }

        await ((Task)handler.DynamicInvoke(command, serviceProvider, cancellationToken)!);
    }
}
