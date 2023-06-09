using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PublisherConfirmAsync.Contracts;
using RabbitMQ.Client;

namespace PublisherConfirmAsync;

public class AsyncPublisherConfirm : IPublisher
{
    private readonly ILogger<AsyncPublisherConfirm> _logger;
    private readonly RabbitMqOptions _rabbitmqOptions;

    private readonly ConcurrentDictionary<ulong, EnvelopMessage> _messagesDeliveryTagsDictionary =
        new();

    public AsyncPublisherConfirm(
        IOptions<RabbitMqOptions> rabbitmqOptions,
        ILogger<AsyncPublisherConfirm> logger
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

    public async Task PublishAsync(IEnumerable<EnvelopMessage> envelopMessages)
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

        var startTime = Stopwatch.GetTimestamp();

        channel.BasicAcks += (_, ea) =>
        {
            var envelop = GetMappedMessage(ea.DeliveryTag);
            _logger.LogInformation(
                $"Message with delivery tag '{
										 ea.DeliveryTag
									 }' and messageId: {
										 envelop?.Message.MessageId
									 } ack-ed, multiple is {
										 ea.Multiple
									 }."
            );

            RemovedConfirmedMessage(ea.DeliveryTag, ea.Multiple);
        };

        channel.BasicNacks += (_, ea) =>
        {
            var envelop = GetMappedMessage(ea.DeliveryTag);
            _logger.LogInformation(
                $"Message with delivery tag '{
										  ea.DeliveryTag
									  }' and messageId: {
										  envelop?.Message.MessageId
									  } nack-ed, multiple is {
										  ea.Multiple
									  }."
            );

            if (envelop is { })
                unsuccessfulPublishedMessages.Enqueue(envelop);

            RemovedConfirmedMessage(ea.DeliveryTag, ea.Multiple);
        };

        var list = envelopMessages.ToList();
        foreach (var envelopMessage in list)
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

            _messagesDeliveryTagsDictionary.TryAdd(currentSequenceNumber, envelopMessage);

            channel.BasicPublish(
                exchange: string.Empty,
                routingKey: envelopMessage.Message.GetType().Name.Underscore(),
                basicProperties: null,
                body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelopMessage.Message))
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
        }

        await WaitUntilConditionMet(
            () => Task.FromResult(_messagesDeliveryTagsDictionary.IsEmpty),
            TimeOut,
            "All messages could not be confirmed in 60 seconds"
        );

        if (unsuccessfulPublishedMessages.Any())
            await PublishAsync(unsuccessfulPublishedMessages);

        _logger.LogInformation("All published messages are confirmed");

        var endTime = Stopwatch.GetTimestamp();
        _logger.LogInformation(
            $"Published {
                list.Count
			} messages and handled confirm asynchronously {
				Stopwatch.GetElapsedTime(startTime, endTime).TotalMilliseconds
			} ms"
        );
    }

    private void RemovedConfirmedMessage(ulong sequenceNumber, bool multiple)
    {
        if (multiple)
        {
            var confirmed = _messagesDeliveryTagsDictionary.Where(k => k.Key <= sequenceNumber);
            foreach (var entry in confirmed)
            {
                _messagesDeliveryTagsDictionary.TryRemove(entry.Key, out _);
            }
        }
        else
        {
            _messagesDeliveryTagsDictionary.TryRemove(sequenceNumber, out _);
        }
    }

    private EnvelopMessage? GetMappedMessage(ulong sequenceNumber)
    {
        _messagesDeliveryTagsDictionary.TryGetValue(sequenceNumber, out EnvelopMessage? e);

        return e;
    }

    private async ValueTask WaitUntilConditionMet(
        Func<Task<bool>> conditionToMet,
        int? timeoutSecond = null,
        string? exception = null
    )
    {
        var time = timeoutSecond ?? 300;

        var startTime = DateTime.Now;
        var timeoutExpired = false;
        var meet = await conditionToMet.Invoke();
        while (!meet)
        {
            if (timeoutExpired)
            {
                throw new TimeoutException(
                    exception ?? $"Condition not met for the test in the '{timeoutExpired}' second."
                );
            }

            await Task.Delay(100);
            meet = await conditionToMet.Invoke();
            timeoutExpired = DateTime.Now - startTime > TimeSpan.FromSeconds(time);
        }
    }
}
