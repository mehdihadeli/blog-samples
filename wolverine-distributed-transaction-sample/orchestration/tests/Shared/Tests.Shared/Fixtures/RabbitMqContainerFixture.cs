using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Tests.Shared.Fixtures;

public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private const string LocalRabbitMqImage = "rabbitmq:3.13-management";
    private bool _started;

    public RabbitMqContainer Container { get; } =
        new RabbitMqBuilder()
            .WithImage(LocalRabbitMqImage)
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

    public string ConnectionString => Container.GetConnectionString();

    public async Task EnsureStartedAsync()
    {
        if (_started) return;
        await Container.StartAsync();
        _started = true;
    }

    public async Task CleanupQueuesAsync(CancellationToken cancellationToken = default)
    {
        if (!_started) return;

        var connectionFactory = new ConnectionFactory { Uri = new Uri(ConnectionString) };
        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        foreach (var queueName in new[] { "payment-requests", "order-payment-responses", "order-payment-responses_error", "order-payment-responses-dlq" })
        {
            try { await channel.QueueDeleteAsync(queueName, false, false, cancellationToken); }
            catch { /* best effort cleanup */ }
        }
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_started) await Container.DisposeAsync();
    }
}
