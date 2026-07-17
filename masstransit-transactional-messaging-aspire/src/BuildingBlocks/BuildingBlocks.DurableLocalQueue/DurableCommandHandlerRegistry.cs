using System.Collections.Concurrent;
using BuildingBlocks.Abstractions.Messages;

namespace BuildingBlocks.DurableLocalQueue;

/// <summary>
/// Non-generic static holder for durable command handlers.
/// Used by <see cref="DurableCommandProcessor{TDbContext}"/> to resolve handlers
/// regardless of which DbContext type is in use.
/// </summary>
internal static class DurableCommandHandlerRegistry
{
    private static readonly ConcurrentDictionary<Type, Delegate> Handlers = new();

    internal static void Register<T>(Func<T, IServiceProvider, CancellationToken, Task> handler)
        where T : class, IInternalCommand
    {
        Handlers[typeof(T)] = handler;
    }

    internal static bool TryGet(Type type, out Delegate? handler)
    {
        return Handlers.TryGetValue(type, out handler);
    }
}
