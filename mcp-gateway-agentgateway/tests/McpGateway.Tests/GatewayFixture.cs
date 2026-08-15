namespace McpGateway.Tests;

/// <summary>
/// One shared MCP client for all tests in the "gateway" collection, so the
/// gateway URL / API key are resolved once from the environment:
///   GATEWAY_URL  (default http://localhost:18080)
///   MCP_API_KEY  (default sk-mcp-gateway-demo-key)
/// </summary>
[CollectionDefinition("gateway")]
public sealed class GatewayCollection : ICollectionFixture<GatewayFixture>;

public sealed class GatewayFixture : IDisposable
{
    internal McpGatewayClient Client { get; }

    public GatewayFixture()
    {
        var url = Environment.GetEnvironmentVariable("GATEWAY_URL") ?? "http://localhost:18080";
        var key = Environment.GetEnvironmentVariable("MCP_API_KEY") ?? "sk-mcp-gateway-demo-key";
        Client = new McpGatewayClient(url, key);
    }

    public void Dispose() => Client.Dispose();
}

/// <summary>
/// Base for all gateway tests: skips the suite (xUnit v3 dynamic skip) when
/// the gateway is not reachable, instead of failing with a connection error.
/// </summary>
public abstract class GatewayTestBase(GatewayFixture fixture)
{
    protected GatewayFixture Fixture { get; } = fixture;

    protected async Task RequireGatewayAsync()
    {
        if (!await Fixture.Client.IsAvailableAsync())
        {
            Assert.Skip(
                "Gateway not reachable — start the stack first (scripts/start-toolhive.sh)"
            );
        }
    }
}
