using BuildingBlocks.Abstractions.Messages;
using BuildingBlocks.Integration.MassTransit.Abstractions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace BuildingBlocks.DurableLocalQueue;

/// <summary>
/// Durable internal command bus that persists commands to a database table
/// within the same transaction as domain changes. A background processor
/// (<see cref="DurableCommandProcessor"/>) picks them up and dispatches them.
/// This is the Wolverine-style "durable local queue" pattern.
/// </summary>
internal sealed class DurableInternalCommandBus<TDbContext>(
    TDbContext dbContext,
    DurableCommandProcessorOptions options
) : IInternalCommandBus
    where TDbContext : DbContext
{
    public async Task EnqueueAsync<T>(T command, CancellationToken cancellationToken = default)
        where T : class, IInternalCommand
    {
        var json = JsonConvert.SerializeObject(
            command,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }
        );

        var entity = new DurableMessage
        {
            TypeName = typeof(T).AssemblyQualifiedName!,
            Payload = json,
            Status = DurableMessageStatus.Pending,
        };

        dbContext.Set<DurableMessage>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
