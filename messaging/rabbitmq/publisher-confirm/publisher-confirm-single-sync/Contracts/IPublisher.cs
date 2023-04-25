namespace PublisherConfirmSingleSync.Contracts;

public interface IPublisher
{
	int TimeOut { get; set; }

	Task PublishAsync<T>(T message)
	where T : EnvelopMessage;

	Task PublishAsync<T>(IEnumerable<T> envelopMessages)
	where T : EnvelopMessage;
}