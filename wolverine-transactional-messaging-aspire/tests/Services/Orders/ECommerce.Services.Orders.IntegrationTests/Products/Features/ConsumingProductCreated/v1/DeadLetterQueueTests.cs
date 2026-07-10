using System.Text.Json;
using ECommerce.Services.Orders.Products.Features.ConsumingProductCreated.v1;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using ECommerce.Services.Shared.Contracts.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;

namespace ECommerce.Services.Orders.IntegrationTests.Products.Features.ConsumingProductCreated.v1;

public class DeadLetterQueueTests(
    PostgresContainerFixture postgres,
    RabbitMqContainerFixture rabbitMq,
    KafkaContainerFixture kafka
) : OrdersIntegrationTestBase(postgres, rabbitMq, kafka)
{
    protected override string MessagingTransport => "rabbitmq";

    protected override void ConfigureFactory(CustomWebApplicationFactory<Program> factory)
    {
        base.ConfigureFactory(factory);
        factory.WithSetting("Wolverine:UseDurableInboxOnAllListeners", "false");
    }

    [Fact]
    public async Task FaultyRabbitMqMessage_ShouldBeMovedToDeadLetterQueue()
    {
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        using var client = Factory.CreateClient();

        await WaitForRabbitMqQueueAsync(
            MessagingConstants.ProductCreatedQueue,
            TimeSpan.FromSeconds(30)
        );

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

        var connectionFactory = new ConnectionFactory { Uri = new Uri(RabbitMq.ConnectionString) };
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Headers = new Dictionary<string, object?>
            {
                ["test-message-id"] = messageId.ToString(),
            },
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: MessagingConstants.ProductCreatedQueue,
            mandatory: false,
            basicProperties: properties,
            body: body
        );

        var deadLetterCount = await WaitForRabbitMqDeadLetterMessageAsync(
            messageId,
            TimeSpan.FromSeconds(30)
        );

        Assert.True(
            deadLetterCount > 0,
            "Expected the faulty message to be moved to the RabbitMQ dead-letter queue."
        );
    }

    private async Task WaitForRabbitMqQueueAsync(string queueName, TimeSpan timeout)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(RabbitMq.ConnectionString) };

        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            await using var connection = await connectionFactory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            try
            {
                await channel.QueueDeclarePassiveAsync(queueName);
                return;
            }
            catch (OperationInterruptedException)
            {
                await Task.Delay(500);
            }
        }

        throw new TimeoutException($"RabbitMQ queue '{queueName}' was not provisioned in time.");
    }

    private async Task<int> WaitForRabbitMqDeadLetterMessageAsync(Guid messageId, TimeSpan timeout)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(RabbitMq.ConnectionString) };

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var result = await channel.BasicGetAsync(
                    MessagingConstants.DeadLetterQueueName,
                    autoAck: true
                );
                if (result is null)
                {
                    await Task.Delay(500);
                    continue;
                }

                var headers = result.BasicProperties.Headers;
                if (headers is null || !headers.TryGetValue("test-message-id", out var value))
                {
                    continue;
                }

                var headerText = value switch
                {
                    byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                    string text => text,
                    _ => null,
                };

                if (Guid.TryParse(headerText, out var parsedId) && parsedId == messageId)
                {
                    return 1;
                }
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
            {
                await Task.Delay(500);
                continue;
            }
        }

        return 0;
    }
}
