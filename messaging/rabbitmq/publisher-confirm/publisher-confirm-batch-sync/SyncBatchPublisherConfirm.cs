using System.Diagnostics;
using System.Text;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PublisherConfirmBatchSync.Contracts;
using RabbitMQ.Client;

namespace PublisherConfirmBatchSync;

public class SyncBatchPublisherConfirm : IPublisher
{
	private readonly ILogger<SyncBatchPublisherConfirm> _logger;
	private readonly RabbitMqOptions _rabbitmqOptions;

	public SyncBatchPublisherConfirm(
		IOptions<RabbitMqOptions> rabbitmqOptions,
		ILogger<SyncBatchPublisherConfirm> logger)
	{
		_logger = logger;
		_rabbitmqOptions = rabbitmqOptions.Value;
	}

	public int TimeOut { get; set; } = 60;

	public async Task PublishAsync<T>(T message)
	where T : EnvelopMessage
	{
		await PublishAsync(new List<T> {message});
	}

	public Task PublishAsync<T>(IEnumerable<T> envelopMessages)
	where T : EnvelopMessage
	{
		var factory = new ConnectionFactory
					  {
						  HostName = _rabbitmqOptions.Host,
						  UserName = _rabbitmqOptions.User,
						  Password = _rabbitmqOptions.Password
					  };

		using var connection = factory.CreateConnection();
		using var channel = connection.CreateModel();

		channel.QueueDeclare(
			queue: typeof(T).Name.Underscore(),
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: null);

		// with calling `ConfirmSelect` on the channel `NextPublishSeqNo` will be set to '1'
		channel.ConfirmSelect();

		_logger.LogInformation($"Start SequenceNumber for 'ConfirmSelect' is: {channel.NextPublishSeqNo}");

		var startTime = Stopwatch.GetTimestamp();

		channel.BasicAcks +=
			(sender, ea) =>
			{
				_logger.LogInformation(
					$"Message with delivery tag '{ea.DeliveryTag}' ack-ed, multiple is {ea.Multiple}.");
			};

		channel.BasicNacks +=
			(sender, ea) =>
			{
				_logger.LogInformation(
					$"Message with delivery tag '{ea.DeliveryTag}' nack-ed, multiple is {ea.Multiple}.");
			};

		var messageList = envelopMessages.ToList();
		foreach (var envelopMessage in messageList)
		{
			var properties = channel.CreateBasicProperties();
			properties.Persistent = true;
			properties.Headers = envelopMessage.Metadata;

			var currentSequenceNumber = channel.NextPublishSeqNo;

			// After publishing publish message sequence number will be incremented
			channel.BasicPublish(
				exchange: string.Empty,
				routingKey: typeof(T).Name.Underscore(),
				basicProperties: properties,
				body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelopMessage)));

			var nextSequenceNumberAfterPublish = channel.NextPublishSeqNo;

			_logger.LogInformation(
				$"message with messageId: {
					envelopMessage.Message.MessageId
				} published, and current SequenceNumber is: {
					currentSequenceNumber
				}, Next SequenceNumber after publishing is: {
					nextSequenceNumberAfterPublish
				}.");
		}

		// batch confirmation, after batch publishing
		channel.WaitForConfirmsOrDie(timeout: TimeSpan.FromSeconds(5));

		_logger.LogInformation("All published messages are confirmed");

		var endTime = Stopwatch.GetTimestamp();
		_logger.LogInformation(
			$"Published {
				messageList.Count
			} messages and handled confirm asynchronously {
				Stopwatch.GetElapsedTime(startTime, endTime).TotalMilliseconds
			} ms");

		return Task.CompletedTask;
	}
}