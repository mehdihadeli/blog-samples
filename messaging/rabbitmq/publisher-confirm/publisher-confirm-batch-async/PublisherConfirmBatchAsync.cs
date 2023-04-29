using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Humanizer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PublisherConfirmBatchAsync.Contracts;
using RabbitMQ.Client;

namespace PublisherConfirmBatchAsync;

public class PublisherConfirmBatchAsync : IPublisher
{
    private readonly ILogger<PublisherConfirmBatchAsync> _logger;
    private readonly RabbitMqOptions _rabbitmqOptions;

    private readonly ConcurrentDictionary<ulong, EnvelopMessage> _messagesDeliveryTagsDictionary =
        new();

    public PublisherConfirmBatchAsync(
        IOptions<RabbitMqOptions> rabbitmqOptions,
        ILogger<PublisherConfirmBatchAsync> logger
    )
    {
        _logger = logger;
        _rabbitmqOptions = rabbitmqOptions.Value;
    }

    public int TimeOut { get; set; } = 60;
    public int BatchSize { get; set; } = 100;

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

        var messageList = envelopMessages.ToList();
        Queue<EnvelopMessage> batchQueue = new Queue<EnvelopMessage>();
        ulong currentSequenceNumber = channel.NextPublishSeqNo; // 1

        foreach (var envelopMessage in messageList)
        {
            batchQueue.Enqueue(envelopMessage);

            if (
                batchQueue.Count == BatchSize
                || (
                    batchQueue.Count != BatchSize
                    && ((batchQueue.Count - 1) + (int)currentSequenceNumber == messageList.Count)
                )
            )
            {
                currentSequenceNumber = PublishBatch(channel, batchQueue, currentSequenceNumber);
                channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(50));

                batchQueue = new Queue<EnvelopMessage>();
            }
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
				messageList.Count
			} messages and handled confirm asynchronously {
				Stopwatch.GetElapsedTime(startTime, endTime).TotalMilliseconds
			} ms"
        );
    }

    private ulong PublishBatch(
        IModel channel,
        IEnumerable<EnvelopMessage> envelopMessages,
        ulong currentSequenceNumber = 1
    )
    {
        // Create a batch of messages
        var batch = channel.CreateBasicPublishBatch();
        var batchMessages = envelopMessages.ToList();
        foreach (var envelope in batchMessages)
        {
            channel.QueueDeclare(
                queue: envelope.Message.GetType().Name.Underscore(),
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.Headers = envelope.Metadata;
            properties.ContentType = "application/json";
            properties.Type = TypeMapper.GetTypeName(envelope.Message.GetType());
            properties.MessageId = envelope.Message.MessageId.ToString();

            var body = new ReadOnlyMemory<byte>(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope.Message))
            );

            batch.Add(
                exchange: string.Empty,
                routingKey: envelope.Message.GetType().Name.Underscore(),
                mandatory: true,
                properties: properties,
                body: body
            );

            _messagesDeliveryTagsDictionary.TryAdd(currentSequenceNumber++, envelope);
        }

        // Publish the batch of messages in a single transaction, After publishing publish messages sequence number will be incremented. internally will assign `NextPublishSeqNo` for each message and them to pendingDeliveryTags collection
        batch.Publish();

        return channel.NextPublishSeqNo;
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
