namespace McpGateway.Tests;

/// <summary>
/// [3]/[5] /fetch route — docker mcp/fetch server behind the gateway.
/// </summary>
[Collection("gateway")]
public class FetchRouteTests(GatewayFixture fixture) : GatewayTestBase(fixture)
{
    [Fact]
    public async Task List_tools_returns_fetch()
    {
        await RequireGatewayAsync();

        var session = await Fixture.Client.OpenSessionAsync("/fetch");
        var result = await Fixture.Client.RpcAsync("/fetch", session, "tools/list", null);
        var names = McpTestHelpers.ToolNames(result);

        names.ShouldContain("fetch");
    }
}
