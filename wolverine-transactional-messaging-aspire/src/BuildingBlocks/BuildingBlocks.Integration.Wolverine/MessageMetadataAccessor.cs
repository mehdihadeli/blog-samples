namespace BuildingBlocks.Integration.Wolverine;

using BuildingBlocks.Abstractions.Messages;

internal sealed class MessageMetadataAccessor : IMessageMetadataAccessor
{
    public Guid GetCorrelationId()
    {
        return Guid.NewGuid();
    }

    public Guid? GetCausationId()
    {
        return null;
    }
}
