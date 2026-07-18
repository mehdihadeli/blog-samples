using Testcontainers.RabbitMq;
using Xunit;

namespace Tests.Shared.Fixtures;

public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private const string LocalRabbitMqImage = "rabbitmq:4-management";
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
        if (_started)
        {
            return;
        }

        await Container.StartAsync();
        _started = true;
    }

    public async Task CleanupQueuesAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            return;
        }

        int managementPort;
        try
        {
            managementPort = Container.GetMappedPublicPort(15672);
        }
        catch (NullReferenceException)
        {
            // Some testcontainer starts expose AMQP correctly but do not publish the management port.
            return;
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{Container.Hostname}:{managementPort}"),
        };

        var credentials = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes("guest:guest")
        );
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var response = await client.GetAsync("/api/queues", cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var queues = System.Text.Json.JsonSerializer.Deserialize<List<RabbitMqQueueInfo>>(content);
        if (queues is null)
        {
            return;
        }

        foreach (var queue in queues)
        {
            var vhost = Uri.EscapeDataString(queue.VHost ?? "/");
            var name = Uri.EscapeDataString(queue.Name ?? string.Empty);
            await client.DeleteAsync($"/api/queues/{vhost}/{name}/contents", cancellationToken);
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

    private sealed class RabbitMqQueueInfo
    {
        public string? Name { get; init; }

        public string? VHost { get; init; }
    }
}
