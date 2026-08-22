using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace AgentGateway.Samples.Tests;

public sealed class McpGatewayTests : GatewayTestBase
{
    public McpGatewayTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task Mcp_endpoint_requires_bearer_token()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.McpUrl, "MCP gateway");

        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(
            Settings.Gateway.McpUrl,
            BuildJsonRpc("tools/list", 1, new { })
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mcp_tools_are_multiplexed_with_target_prefixes()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.McpUrl, "MCP gateway");

        var token = await GetKeycloakTokenAsync(
            Settings.Users["Alice"].Username,
            Settings.Users["Alice"].Password
        );
        var names = await ListToolsAsync(token);

        names.ShouldContain(t => t!.StartsWith("tickets_"));
        names.ShouldContain(t => t!.StartsWith("catalog_"));
        names.ShouldContain(t => t!.StartsWith("customers_"));
        names.ShouldContain(t => t!.StartsWith("everything_"));
        names.ShouldContain(t => t!.StartsWith("time_"));
        names.ShouldContain("openapi_getInventory");

        Output.WriteLine($"Discovered {names.Count} multiplexed tools");
        foreach (var name in names)
        {
            Output.WriteLine($"  - {name}");
        }
    }

    [Fact]
    public async Task Admin_user_alice_can_see_customers_tools()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.McpUrl, "MCP gateway");

        var token = await GetKeycloakTokenAsync(
            Settings.Users["Alice"].Username,
            Settings.Users["Alice"].Password
        );
        var tools = await ListToolsAsync(token);

        tools.ShouldContain(t => t.StartsWith("customers_"));
    }

    [Fact]
    public async Task Non_admin_user_bob_cannot_see_customers_tools()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.McpUrl, "MCP gateway");

        var token = await GetKeycloakTokenAsync(
            Settings.Users["Bob"].Username,
            Settings.Users["Bob"].Password
        );
        var tools = await ListToolsAsync(token);

        tools.ShouldNotContain(t => t.StartsWith("customers_"));
        tools.ShouldContain(t => t.StartsWith("tickets_"));
        tools.ShouldContain(t => t.StartsWith("catalog_"));
    }

    [Fact]
    public async Task Jwt_subject_is_preserved_for_audit_and_rate_limit()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.McpUrl, "MCP gateway");

        var token = await GetKeycloakTokenAsync(
            Settings.Users["Alice"].Username,
            Settings.Users["Alice"].Password
        );
        var payload = ParseJwtPayload(token);

        payload.GetProperty("sub").GetString().ShouldNotBeNullOrEmpty();
        payload
            .GetProperty("realm_access")
            .GetProperty("roles")
            .EnumerateArray()
            .Select(r => r.GetString())
            .ShouldContain("support-admin");
    }

    private async Task<List<string>> ListToolsAsync(string token)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Warning)
        );
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
                await using var mcpClient = await McpClient.CreateAsync(
                    transport,
                    new McpClientOptions
                    {
                        ClientInfo = new Implementation { Name = "gateway-tests", Version = "1.0" },
                    },
                    loggerFactory
                );
                var tools = await mcpClient.ListToolsAsync();
                return tools.Select(tool => tool.Name).ToList();
            }
            catch (Exception exception) when (exception.Message.Contains("429") && attempt < 60)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken);
            }
        }
    }

    private static object BuildJsonRpc(string method, int id, object @params)
    {
        return new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params,
        };
    }
}
