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
        await using var client = await CreateMcpClientAsync(token, loggerFactory);

        // Other integration tests share this gateway-wide bucket. Retry while
        // it refills, then verify ordinary traffic is accepted.
        for (var i = 0; i < 5; i++)
        {
            for (var attempt = 0; attempt < 15; attempt++)
            {
                try
                {
                    var tools = await client.ListToolsAsync();
                    tools.ShouldNotBeEmpty();
                    break;
                }
                catch (Exception exception) when (exception.Message.Contains("429"))
                {
                    if (attempt == 14)
                    {
                        throw;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken);
                }
            }
        }
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
        await using var client = await CreateMcpClientAsync(token, loggerFactory);

        var tooManyRequestsObserved = false;
        var requestCount = 0;

        // The MCP gateway allows 2000 requests/minute. Drain the bucket quickly.
        for (; requestCount < 2020; requestCount++)
        {
            try
            {
                var tools = await client.ListToolsAsync();
                tools.ShouldNotBeEmpty();
            }
            catch (Exception exception)
            {
                tooManyRequestsObserved = exception.Message.Contains("429");
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

        return await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "gateway-rate-tests", Version = "1.0" },
            },
            loggerFactory
        );
    }
}
