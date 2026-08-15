namespace McpGateway.Tests;

/// <summary>
/// [4]/[9] /thinking route — docker mcp/sequentialthinking behind the gateway.
/// </summary>
[Collection("gateway")]
public class ThinkingRouteTests(GatewayFixture fixture) : GatewayTestBase(fixture)
{
    [Fact]
    public async Task List_tools_returns_sequentialthinking()
    {
        await RequireGatewayAsync();

        var session = await Fixture.Client.OpenSessionAsync("/thinking");
        var result = await Fixture.Client.RpcAsync("/thinking", session, "tools/list", null);
        var names = McpTestHelpers.ToolNames(result);

        names.ShouldContain("sequentialthinking");
    }

    [Fact]
    public async Task Call_sequentialthinking_returns_thought_number()
    {
        await RequireGatewayAsync();

        var session = await Fixture.Client.OpenSessionAsync("/thinking");
        var result = await Fixture.Client.RpcAsync(
            "/thinking",
            session,
            "tools/call",
            new
            {
                name = "sequentialthinking",
                arguments = new
                {
                    thought = "First step of a multi-step analysis",
                    nextThoughtNeeded = true,
                    thoughtNumber = 1,
                    totalThoughts = 2,
                },
            }
        );

        var text = result.ToString();
        text.ShouldContain("thoughtNumber");
        text.ShouldContain("1");
    }
}
