using System.Diagnostics;
using System.Text;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PublisherConfirmBatchSync;
using PublisherConfirmSingleSync.Contracts;
using RabbitMQ.Client;

namespace PublisherConfirmSingleSync;

public class SyncSinglePublisherConfirm : IPublisher
{
    private readonly ILogger<SyncSinglePublisherConfirm> _logger;
    private readonly RabbitMqOptions _rabbitmqOptions;

    public SyncSinglePublisherConfirm(
        IOptions<RabbitMqOptions> rabbitmqOptions,
        ILogger<SyncSinglePublisherConfirm> logger
    )
    {
        _logger = logger;
        _rabbitmqOptions = rabbitmqOptions.Value;
    }

    public int TimeOut { get; set; } = 60;

    public async Task PublishAsync(EnvelopMessage message)
    {
        await PublishAsync(new List<EnvelopMessage> { message });
    }

    public async Task PublishAsync(IEnumerable<EnvelopMessage> messages)
    {
        Queue<EnvelopMessage> unsuccessfulPublishedMessages = new Queue<EnvelopMessage>();

        var factory = new ConnectionFactory
        {
            HostName = _rabbitmqOptions.Host,
            UserName = _rabbitmqOptions.User,
            Password = _rabbitmqOptions.Password
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // with calling `ConfirmSelect` on the channel `NextPublishSeqNo` will be set to '1'
        channel.ConfirmSelect();

        _logger.LogInformation(
            $"Start SequenceNumber for 'ConfirmSelect' is: {channel.NextPublishSeqNo}"
        );

        channel.BasicAcks += (sender, ea) =>
        {
            _logger.LogInformation(
                $"Message with delivery tag '{ea.DeliveryTag}' ack-ed, multiple is {ea.Multiple}."
            );
        };

        channel.BasicNacks += (sender, ea) =>
        {
            _logger.LogInformation(
                $"Message with delivery tag '{ea.DeliveryTag}' nack-ed, multiple is {ea.Multiple}."
            );
        };

        var startTime = Stopwatch.GetTimestamp();

        var messageList = messages.ToList();
        foreach (var envelopMessage in messageList)
        {
            channel.QueueDeclare(
                queue: envelopMessage.Message.GetType().Name.Underscore(),
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.Headers = envelopMessage.Metadata;
            properties.ContentType = "application/json";
            properties.Type = TypeMapper.GetTypeName(envelopMessage.Message.GetType());
            properties.MessageId = envelopMessage.Message.MessageId.ToString();

            var currentSequenceNumber = channel.NextPublishSeqNo;

            try
            {
                // After publishing publish message sequence number will be incremented
                channel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: envelopMessage.Message.GetType().Name.Underscore(),
                    basicProperties: properties,
                    body: Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(envelopMessage.Message)
                    )
                );

                var nextSequenceNumberAfterPublish = channel.NextPublishSeqNo;

                _logger.LogInformation(
                    $"message with messageId: {
						envelopMessage.Message.MessageId
					} published, and current SequenceNumber is: {
						currentSequenceNumber
					}, next SequenceNumber after publishing is: {
						nextSequenceNumberAfterPublish
					}."
                );

                // single confirmation after each publish
                channel.WaitForConfirmsOrDie(timeout: TimeSpan.FromSeconds(5));

                _logger.LogInformation(
                    $"message with messageId: {
						envelopMessage.Message.MessageId
					}, and SequenceNumber is: {
						currentSequenceNumber
					} confirmed."
                );
            }
            catch (Exception ex)
            {
                var nextSequenceNumberAfterPublish = channel.NextPublishSeqNo;

                _logger.LogInformation(
                    $"message with messageId: {
						envelopMessage.Message.MessageId
					} failed, and current SequenceNumber is: {
						currentSequenceNumber
					}, next SequenceNumber after publishing is: {
						nextSequenceNumberAfterPublish
					}."
                );
                unsuccessfulPublishedMessages.Enqueue(envelopMessage);
            }
        }

        if (unsuccessfulPublishedMessages.Any())
        {
            await PublishAsync(unsuccessfulPublishedMessages);
        }

        _logger.LogInformation("All published messages are confirmed");

        var endTime = Stopwatch.GetTimestamp();
        _logger.LogInformation(
            $"Published {
				messageList.Count
			} messages and handled confirm asynchronously {
				Stopwatch.GetElapsedTime(startTime, endTime).TotalMilliseconds
			} ms"
        );
    }
}
