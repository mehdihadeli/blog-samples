namespace BuildingBlocks.Integration.Wolverine.Configuration;

public sealed class WolverineRetryOptions
{
    public int MaximumAttempts { get; set; } = 3;
}
