using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECommerce.BuildingBlocks.RabbitMQ.IntegrationTests.Infrastructure;

/// <summary>
/// Thin client for the RabbitMQ management HTTP API (plugin enabled by the
/// <c>rabbitmq:4-management</c> image). Used to assert the topology that the
/// building block actually declares on the broker (exchanges, queues, bindings).
/// </summary>
internal sealed class RabbitMqManagementClient : IDisposable
{
    private readonly HttpClient _client;

    public RabbitMqManagementClient(string hostname, int managementPort)
    {
        _client = new HttpClient { BaseAddress = new Uri($"http://{hostname}:{managementPort}") };

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("guest:guest"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            credentials
        );
    }

    public async Task<IReadOnlyList<RabbitMqExchangeInfo>> GetExchangesAsync(
        CancellationToken cancellationToken
    )
    {
        var content = await GetStringAsync("/api/exchanges", cancellationToken);
        return JsonSerializer.Deserialize<List<RabbitMqExchangeInfo>>(content) ?? [];
    }

    public async Task<IReadOnlyList<RabbitMqQueueInfo>> GetQueuesAsync(
        CancellationToken cancellationToken
    )
    {
        var content = await GetStringAsync("/api/queues", cancellationToken);
        return JsonSerializer.Deserialize<List<RabbitMqQueueInfo>>(content) ?? [];
    }

    public async Task<IReadOnlyList<RabbitMqBindingInfo>> GetBindingsAsync(
        CancellationToken cancellationToken
    )
    {
        var content = await GetStringAsync("/api/bindings", cancellationToken);
        return JsonSerializer.Deserialize<List<RabbitMqBindingInfo>>(content) ?? [];
    }

    private async Task<string> GetStringAsync(string path, CancellationToken cancellationToken)
    {
        var response = await _client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public void Dispose() => _client.Dispose();
}

internal sealed class RabbitMqExchangeInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

internal sealed class RabbitMqQueueInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class RabbitMqBindingInfo
{
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("destination")]
    public string? Destination { get; set; }

    [JsonPropertyName("destination_type")]
    public string? DestinationType { get; set; }

    [JsonPropertyName("routing_key")]
    public string? RoutingKey { get; set; }
}
