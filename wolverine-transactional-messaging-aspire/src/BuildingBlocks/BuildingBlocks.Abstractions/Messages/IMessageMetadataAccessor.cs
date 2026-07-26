namespace BuildingBlocks.Abstractions.Messages;

/// <summary>
///     Provides ambient metadata (correlation/causation IDs) for message envelopes.
///     Typically backed by Wolverine middleware, header propagation, or HttpContext.
///     Supported levels: core/utility — used by Levels 1, 3, 4 for envelope metadata population.
/// </summary>
public interface IMessageMetadataAccessor
{
    Guid GetCorrelationId();
    Guid? GetCausationId();
}
