using System.Reflection;

namespace BuildingBlocks.Integration.Wolverine.Configuration;

public sealed class WolverineCommonOptions
{
    public required string DurableStorageConnectionString { get; init; }

    public IReadOnlyCollection<Assembly> HandlerAssemblies { get; init; } = Array.Empty<Assembly>();

    public WolverineBusOptions Bus { get; init; } = new();
}
