using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using ECommerce.Services.Orders.Products.Features.ConsumingProductCreated.v1;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using ECommerce.Services.Shared.Contracts.Messaging;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;

namespace ECommerce.Services.Orders.IntegrationTests.Products.Features.ConsumingProductCreated.v1;

public class KafkaDeadLetterQueueTests(
    PostgresContainerFixture postgres,
    RabbitMqContainerFixture rabbitMq,
    KafkaContainerFixture kafka
) : OrdersIntegrationTestBase(postgres, rabbitMq, kafka)
{
    protected override string MessagingTransport => "kafka";

    protected override void ConfigureFactory(CustomWebApplicationFactory<Program> factory)
    {
        base.ConfigureFactory(factory);
        factory.WithSetting("Wolverine:UseDurableInboxOnAllListeners", "false");
    }

    [Fact]
    public async Task FaultyKafkaMessage_ShouldBeMovedToDeadLetterTopic()
    {
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        using var client = Factory.CreateClient();

        await WaitForKafkaTopicAsync(
            MessagingConstants.ProductCreatedTopic,
            TimeSpan.FromSeconds(30)
        );
        await WaitForKafkaTopicAsync(
            MessagingConstants.DeadLetterQueueName,
            TimeSpan.FromSeconds(30)
        );
        await Task.Delay(TimeSpan.FromSeconds(10));

        var envelope = MessageEnvelope.Create(
            new ProductCreatedV1(
                productId,
                ProductCreatedFaultyHandler.MessageTypes.FaultyProductCreated,
                "Faulty Basket",
                9.99m,
                occurredAt
            ),
            correlationId,
            messageId,
            occurredAt
        );

        var producerConfig = new ProducerConfig { BootstrapServers = Kafka.BootstrapServers };

        using var producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        await producer.ProduceAsync(
            MessagingConstants.ProductCreatedTopic,
            new Message<Null, string>
            {
                Value = JsonSerializer.Serialize(envelope),
                Headers = new Headers
                {
                    new Header(
                        "content-type",
                        System.Text.Encoding.UTF8.GetBytes("application/json")
                    ),
                    new Header(
                        "test-message-id",
                        System.Text.Encoding.UTF8.GetBytes(messageId.ToString())
                    ),
                },
            }
        );

        var deadLetterCount = await WaitForKafkaDeadLetterMessageAsync(
            messageId,
            TimeSpan.FromSeconds(45)
        );

        Assert.True(
            deadLetterCount > 0,
            "Expected the faulty message to be moved to the Kafka dead-letter topic."
        );
    }

    private async Task WaitForKafkaTopicAsync(string topicName, TimeSpan timeout)
    {
        var config = new AdminClientConfig { BootstrapServers = Kafka.BootstrapServers };

        using var client = new AdminClientBuilder(config).Build();

        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var metadata = client.GetMetadata(TimeSpan.FromSeconds(5));
                if (
                    metadata.Topics.Any(topic =>
                        string.Equals(topic.Topic, topicName, StringComparison.Ordinal)
                    )
                )
                {
                    return;
                }
            }
            catch (KafkaException)
            {
                // Topic metadata can lag while Wolverine provisions transport topology.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Kafka topic '{topicName}' was not provisioned in time.");
    }

    private async Task<int> WaitForKafkaDeadLetterMessageAsync(Guid messageId, TimeSpan timeout)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = Kafka.BootstrapServers,
            GroupId = $"dlq-test-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(MessagingConstants.DeadLetterQueueName);

        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            ConsumeResult<Ignore, string>? result = null;
            try
            {
                result = consumer.Consume(TimeSpan.FromSeconds(1));
            }
            catch (ConsumeException)
            {
                // Topic may not exist yet; retry.
            }
            catch (KafkaException)
            {
                // Transient Kafka error; retry.
            }

            if (result is null)
            {
                continue;
            }

            if (result.Message.Headers is null)
            {
                continue;
            }

            var header = result.Message.Headers.TryGetLastBytes("test-message-id", out var bytes)
                ? System.Text.Encoding.UTF8.GetString(bytes)
                : null;

            if (Guid.Parse(header ?? string.Empty) == messageId)
            {
                return 1;
            }
        }

        return 0;
    }
}
