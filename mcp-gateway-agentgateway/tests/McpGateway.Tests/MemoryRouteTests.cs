namespace McpGateway.Tests;

/// <summary>
/// [2]/[6] /memory route — docker mcp/memory server behind the gateway.
/// </summary>
[Collection("gateway")]
public class MemoryRouteTests(GatewayFixture fixture) : GatewayTestBase(fixture)
{
    private static readonly string[] ExpectedMemoryTools =
    [
        "create_entities",
        "create_relations",
        "add_observations",
        "read_graph",
        "search_nodes",
        "open_nodes",
    ];

    [Fact]
    public async Task List_tools_returns_memory_toolkit()
    {
        await RequireGatewayAsync();

        var session = await Fixture.Client.OpenSessionAsync("/memory");
        var result = await Fixture.Client.RpcAsync("/memory", session, "tools/list", null);
        var names = McpTestHelpers.ToolNames(result);

        foreach (var tool in ExpectedMemoryTools)
        {
            names.ShouldContain(tool);
        }
    }

    [Fact]
    public async Task Create_entities_then_read_graph_returns_entities()
    {
        await RequireGatewayAsync();

        var session = await Fixture.Client.OpenSessionAsync("/memory");

        await Fixture.Client.RpcAsync(
            "/memory",
            session,
            "tools/call",
            new
            {
                name = "create_entities",
                arguments = new
                {
                    entities = new object[]
                    {
                        new
                        {
                            name = "Alice",
                            entityType = "person",
                            observations = new[] { "Works at Acme", "Likes coffee" },
                        },
                        new
                        {
                            name = "Bob",
                            entityType = "person",
                            observations = new[] { "Works at Acme" },
                        },
                    },
                },
            }
        );

        var graph = await Fixture.Client.RpcAsync(
            "/memory",
            session,
            "tools/call",
            new { name = "read_graph", arguments = new { } }
        );

        var text = graph.ToString();
        text.ShouldContain("Alice");
        text.ShouldContain("Bob");
    }
}
