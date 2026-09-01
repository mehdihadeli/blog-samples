using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace AgentGateway.Samples.Tests;

public sealed class ZZRateLimitTests : GatewayTestBase
{
    public ZZRateLimitTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task Normal_load_is_allowed_by_local_rate_limit()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.McpUrl, "MCP gateway");

        var token = await GetKeycloakTokenAsync(
            Settings.Users["Alice"].Username,
            Settings.Users["Alice"].Password
        );
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Warning)
        );
        // Other integration tests share this gateway-wide bucket. Wait for a
        // clean refill, then verify a normal MCP request succeeds.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                await using var client = await CreateMcpClientAsync(token, loggerFactory);
                var tools = await client.ListToolsAsync();
                tools.ShouldNotBeEmpty();
                return;
            }
            catch (Exception exception) when (IsRateLimited(exception) && attempt < 7)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);
            }
        }

        throw new InvalidOperationException(
            "Normal MCP traffic never recovered from rate limiting."
        );
    }

    [Fact]
    public async Task Z_Excessive_burst_to_MCP_eventually_returns_429()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.McpUrl, "MCP gateway");

        var token = await GetKeycloakTokenAsync(
            Settings.Users["Alice"].Username,
            Settings.Users["Alice"].Password
        );
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Warning)
        );
        await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);
        await using var client = await CreateMcpClientAsync(token, loggerFactory);

        var tooManyRequestsObserved = false;
        var requestCount = 0;
        // Keep the burst small enough for the sample request-log store while
        // still exceeding the 60-request local demo bucket.
        for (; requestCount < 100; requestCount++)
        {
            try
            {
                var tools = await client.ListToolsAsync();
                tools.ShouldNotBeEmpty();
            }
            catch (Exception exception)
            {
                tooManyRequestsObserved = IsRateLimited(exception);
                Output.WriteLine(exception.Message);
                break;
            }
        }

        tooManyRequestsObserved.ShouldBeTrue(
            $"Expected at least one 429 after exhausting the token bucket (sent {requestCount} requests)."
        );
    }

    private async Task<McpClient> CreateMcpClientAsync(string token, ILoggerFactory loggerFactory)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(Settings.Gateway.McpUrl),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {token}",
                },
            },
            loggerFactory
        );

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await McpClient.CreateAsync(
                    transport,
                    new McpClientOptions
                    {
                        ClientInfo = new Implementation
                        {
                            Name = "gateway-rate-tests",
                            Version = "1.0",
                        },
                    },
                    loggerFactory
                );
            }
            catch (Exception exception) when (IsRateLimited(exception) && attempt < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);
            }
        }
    }

    private static bool IsRateLimited(Exception exception)
    {
        return exception.Message.Contains("429", StringComparison.Ordinal)
            || exception.Message.Contains(
                "rate limit exceeded",
                StringComparison.OrdinalIgnoreCase
            );
    }
}
