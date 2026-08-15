namespace McpGateway.Tests;

/// <summary>
/// [5]/[7]/[8]/[10] /mcp — federated view of memory + fetch + thinking +
/// everything, tool names prefixed per backend (memory_*, fetch_*,
/// thinking_*, everything_*). The everything target (npm-only server) is
/// present in the stdio and ToolHive variants (4 targets); the mcpwrap
/// variant hosts 3 targets and skips everything_*.
/// </summary>
[Collection("gateway")]
public class FederatedRouteTests(GatewayFixture fixture) : GatewayTestBase(fixture)
{
    [Fact]
    public async Task List_tools_returns_prefixed_federated_set()
    {
        await RequireGatewayAsync();

        var session = await Fixture.Client.OpenSessionAsync("/mcp");
        var result = await Fixture.Client.RpcAsync("/mcp", session, "tools/list", null);
        var names = McpTestHelpers.ToolNames(result);

        names
            .Count(n => n.StartsWith("memory_", StringComparison.Ordinal))
            .ShouldBeGreaterThanOrEqualTo(6);
        names.ShouldContain("fetch_fetch");
        names.ShouldContain("thinking_sequentialthinking");
        names.ShouldContain("everything_echo");
        names
            .Count(n => n.StartsWith("everything_", StringComparison.Ordinal))
            .ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Prefixed_memory_tools_work_end_to_end()
    {
        await RequireGatewayAsync();

        var session = await Fixture.Client.OpenSessionAsync("/mcp");

        await Fixture.Client.RpcAsync(
            "/mcp",
            session,
            "tools/call",
            new
            {
                name = "memory_create_entities",
                arguments = new
                {
                    entities = new object[]
                    {
                        new
                        {
                            name = "Carol",
                            entityType = "person",
                            observations = new[] { "Federated via /mcp" },
                        },
                    },
                },
            }
        );

        var graph = await Fixture.Client.RpcAsync(
            "/mcp",
            session,
            "tools/call",
            new { name = "memory_read_graph", arguments = new { } }
        );

        graph.ToString().ShouldContain("Carol");
    }

    [Fact]
    public async Task Prefixed_fetch_returns_page_content()
    {
        await RequireGatewayAsync();

        var session = await Fixture.Client.OpenSessionAsync("/mcp");
        var result = await Fixture.Client.RpcAsync(
            "/mcp",
            session,
            "tools/call",
            new { name = "fetch_fetch", arguments = new { url = "https://example.com" } }
        );

        result.ToString().ShouldContain("Example Domain");
    }

    [Fact]
    public async Task Prefixed_sequentialthinking_returns_thought_history_length()
    {
        await RequireGatewayAsync();

        var session = await Fixture.Client.OpenSessionAsync("/mcp");
        var result = await Fixture.Client.RpcAsync(
            "/mcp",
            session,
            "tools/call",
            new
            {
                name = "thinking_sequentialthinking",
                arguments = new
                {
                    thought = "Federated sequential thinking step",
                    nextThoughtNeeded = false,
                    thoughtNumber = 1,
                    totalThoughts = 1,
                },
            }
        );

        var text = result.ToString();
        text.ShouldContain("thoughtHistoryLength");
        text.ShouldContain("1");
    }

    [Fact]
    public async Task Prefixed_everything_echo_roundtrips_through_the_virtual_server()
    {
        await RequireGatewayAsync();

        var session = await Fixture.Client.OpenSessionAsync("/mcp");
        var result = await Fixture.Client.RpcAsync(
            "/mcp",
            session,
            "tools/call",
            new { name = "everything_echo", arguments = new { message = "federated echo" } }
        );

        result.ToString().ShouldContain("federated echo");
    }
}
