using Confluent.Kafka;
using Confluent.Kafka.Admin;
using DotNet.Testcontainers.Containers;
using Testcontainers.Kafka;
using Xunit;

namespace Tests.Shared.Fixtures;

public sealed class KafkaContainerFixture : IAsyncLifetime
{
    private const string LocalKafkaImage = "confluentinc/cp-kafka:7.5.12";
    private bool _started;

    public KafkaContainer Container { get; } =
        new KafkaBuilder()
            .WithImage(LocalKafkaImage)
            .WithVendor(KafkaVendor.Confluent)
            .WithKRaft()
            .Build();

    public string BootstrapServers => Container.GetBootstrapAddress();

    public async Task EnsureStartedAsync()
    {
        if (_started)
        {
            return;
        }

        await Container.StartAsync();
        _started = true;
    }

    public async Task CleanupTopicsAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            return;
        }

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

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            await Container.DisposeAsync();
        }
    }
}
