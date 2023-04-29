namespace PublisherConfirmSingleSync.Contracts;

public interface IPublisher
{
	int TimeOut { get; set; }

	Task PublishAsync(EnvelopMessage message);

	Task PublishAsync(IEnumerable<EnvelopMessage> messages);
}