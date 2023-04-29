namespace PublisherConfirmBatchAsync.Contracts;

public class EnvelopMessage
{
	public required IMessage Message { get; init; }
	public IMetadata Metadata { get; init; } = default!;
}