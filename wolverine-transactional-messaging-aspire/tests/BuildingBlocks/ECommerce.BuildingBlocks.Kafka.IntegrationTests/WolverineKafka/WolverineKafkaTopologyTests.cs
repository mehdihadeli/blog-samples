using Confluent.Kafka;
using Confluent.Kafka.Admin;
using ECommerce.BuildingBlocks.TestHost.Messaging;
using Shouldly;

namespace ECommerce.BuildingBlocks.Kafka.IntegrationTests.WolverineKafka;

/// <summary>
/// Verifies the topics the building block actually creates on the broker
/// (via the Kafka Admin API): the explicit <c>PublishToTopic</c> topic
/// (including the <c>TopicSpecification</c> partition count), the auto-named
/// snake_case topic and the custom-naming-convention topic.
/// </summary>
public class WolverineKafkaTopologyTests(KafkaBuildingBlocksSharedFixture sharedFixture)
    : KafkaBuildingBlocksIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task should_create_explicit_topic_with_specified_partitions()
    {
        // PublishToTopic(..., spec => spec.NumPartitions = 1).
        var metadata = await WaitForTopicAsync(
            WolverineKafkaTestTopology.ProductCreatedTopic,
            TestCancellationToken
        );

        metadata.ShouldNotBeNull();
        metadata.Partitions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task should_create_auto_named_snake_case_topic()
    {
        // Publish<OrderCreatedV1>() + Listen<OrderCreatedV1>() →
        // topic "order_created_v1".
        var metadata = await WaitForTopicAsync("order_created_v1", TestCancellationToken);

        metadata.ShouldNotBeNull();
    }

    [Fact]
    public async Task should_create_custom_named_topic()
    {
        // WithNamingConvention → topic "custom-inventory_adjusted_v1".
        var metadata = await WaitForTopicAsync(
            $"{WolverineKafkaTestTopology.CustomTopicPrefix}-inventory_adjusted_v1",
            TestCancellationToken
        );

        metadata.ShouldNotBeNull();
    }

    private async Task<TopicMetadata?> WaitForTopicAsync(
        string topicName,
        CancellationToken cancellationToken
    )
    {
        var bootstrapServers = SharedFixture.Kafka!.BootstrapServers;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var client = new AdminClientBuilder(
                new AdminClientConfig { BootstrapServers = bootstrapServers }
            ).Build();

            var metadata = client.GetMetadata(TimeSpan.FromSeconds(5));

            var topic = metadata.Topics.FirstOrDefault(t =>
                t.Topic == topicName && t.Error.Code == ErrorCode.NoError
            );

            if (topic is not null)
            {
                return topic;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new TimeoutException($"Topic '{topicName}' was not created within the test timeout.");
    }
}
