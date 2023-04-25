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
    private readonly ConcurrentDictionary<ulong, EnvelopMessage> _outstandingConfirms = new();
    private readonly ConcurrentQueue<EnvelopMessage> _republishQueue = new();

    public AsyncPublisherConfirm(IOptions<RabbitMqOptions> rabbitmqOptions, ILogger<AsyncPublisherConfirm> logger)
    {
        _logger = logger;
        _rabbitmqOptions = rabbitmqOptions.Value;
    }

    public int TimeOut { get; set; } = 60;

    public async Task PublishAsync<T>(T message)
        where T : EnvelopMessage
    {
        await PublishAsync(new List<T> { message });
    }

    public async Task PublishAsync<T>(IEnumerable<T> envelopMessages)
        where T : EnvelopMessage
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitmqOptions.Host,
            UserName = _rabbitmqOptions.User,
            Password = _rabbitmqOptions.Password
        };
        using (var connection = factory.CreateConnection())
        {
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(
                    queue: typeof(T).Name.Underscore(),
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                channel.ConfirmSelect();

                channel.BasicAcks += (sender, ea) =>
                {
                    _outstandingConfirms.TryGetValue(
                        ea.DeliveryTag,
                        out EnvelopMessage? envelopMessage
                    );

                    var messageBody = JsonConvert.SerializeObject(envelopMessage?.Message);
                    _logger.LogInformation(
                        $"Message with body {messageBody} has been ack-ed. Sequence number: {ea.DeliveryTag}, multiple: {ea.Multiple}"
                    );

                    CleanOutstandingConfirms(ea.DeliveryTag, ea.Multiple);
                };

                channel.BasicNacks += (sender, ea) =>
                {
                    _outstandingConfirms.TryGetValue(
                        ea.DeliveryTag,
                        out EnvelopMessage? envelopMessage
                    );
                    var messageBody = JsonConvert.SerializeObject(envelopMessage?.Message);
                    _logger.LogInformation(
                        $"Message with body {messageBody} has been nack-ed. Sequence number: {ea.DeliveryTag}, multiple: {ea.Multiple}"
                    );

                    if (envelopMessage != null)
                    {
                        _republishQueue.Enqueue(envelopMessage);
                    }

                    CleanOutstandingConfirms(ea.DeliveryTag, ea.Multiple);
                };

                var startTime = Stopwatch.GetTimestamp();

                var list = envelopMessages.ToList();
                foreach (var envelopMessage in list)
                {
                    var properties = channel.CreateBasicProperties();
                    properties.Persistent = true;
                    properties.Headers = envelopMessage.Metadata;

                    _outstandingConfirms.TryAdd(channel.NextPublishSeqNo, envelopMessage);

                    channel.BasicPublish(
                        exchange: string.Empty,
                        routingKey: typeof(T).Name.Underscore(),
                        basicProperties: null,
                        body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelopMessage))
                    );
                }

                await WaitUntilConditionMet(
                    () => Task.FromResult(_outstandingConfirms.IsEmpty),
                    600,
                    "All messages could not be confirmed in 60 seconds"
                );

                var endTime = Stopwatch.GetTimestamp();
                _logger.LogInformation(
                    $"Published {list.Count} messages and handled confirm asynchronously {Stopwatch.GetElapsedTime(startTime, endTime).TotalMilliseconds} ms"
                );
            }
        }

        if (_republishQueue.Count > 0)
        {
            await PublishAsync(_republishQueue);
        }
    }

    private void CleanOutstandingConfirms(ulong sequenceNumber, bool multiple)
    {
        if (multiple)
        {
            var confirmed = _outstandingConfirms.Where(k => k.Key <= sequenceNumber);
            foreach (var entry in confirmed)
            {
                _outstandingConfirms.TryRemove(entry.Key, out _);
            }
        }
        else
        {
            _outstandingConfirms.TryRemove(sequenceNumber, out _);
        }
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
