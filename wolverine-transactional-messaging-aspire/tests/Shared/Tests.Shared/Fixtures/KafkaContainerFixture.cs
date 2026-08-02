using Confluent.Kafka;
using Confluent.Kafka.Admin;
using DotNet.Testcontainers.Containers;
using Testcontainers.Kafka;
using Xunit;

namespace Tests.Shared.Fixtures;

public sealed class KafkaContainerFixture : IAsyncLifetime
{
    private const string LocalKafkaImage = "confluentinc/cp-kafka:7.5.12";

    public KafkaContainer Container { get; } =
        new KafkaBuilder()
            .WithImage(LocalKafkaImage)
            .WithVendor(KafkaVendor.Confluent)
            .WithKRaft()
            .Build();

    public string BootstrapServers => Container.GetBootstrapAddress();

    public async ValueTask InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        var adminConfig = new AdminClientConfig { BootstrapServers = BootstrapServers };
        using var client = new AdminClientBuilder(adminConfig).Build();

        var metadata = client.GetMetadata(TimeSpan.FromSeconds(5));
        var topics = metadata
            .Topics.Where(topic => !topic.Topic.StartsWith("__", StringComparison.Ordinal))
            .Select(topic => topic.Topic)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (topics.Length == 0)
        {
            return;
        }

        try
        {
            await client.DeleteTopicsAsync(topics, new DeleteTopicsOptions());
        }
        catch (DeleteTopicsException)
        {
            // Kafka metadata can lag while topics are being created or deleted. Tests only need best-effort cleanup.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
