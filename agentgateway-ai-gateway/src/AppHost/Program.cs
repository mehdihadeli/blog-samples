var builder = DistributedApplication.CreateBuilder(args);

// The three first-party MCP tool servers. In production the same binaries are
// deployed as containers and reachable from AgentGateway over the compose
// network. Here Aspire pins their ports for local development.
var mcpTickets = builder
    .AddProject<Projects.Mcp_Tickets>("mcp-tickets")
    .WithHttpEndpoint(port: 8081);

var mcpCatalog = builder
    .AddProject<Projects.Mcp_Catalog>("mcp-catalog")
    .WithHttpEndpoint(port: 8082);

var mcpCustomers = builder
    .AddProject<Projects.Mcp_Customers>("mcp-customers")
    .WithHttpEndpoint(port: 8083);

// The A2A agent. AgentGateway fronts it on port 3001.
var supportAgent = builder
    .AddProject<Projects.SupportAgent>("support-agent")
    .WithHttpEndpoint(port: 9999);

// The chat client. It talks to DeepSeek, the MCP tools, and the A2A agent
// exclusively through AgentGateway, exactly like a production client would.
builder
    .AddProject<Projects.SupportChat>("support-chat")
    .WithEnvironment("GatewayMcpUrl", "http://localhost:3000/mcp")
    .WithEnvironment("GatewayLlmUrl", "http://localhost:4000/v1")
    .WithEnvironment("GatewayA2AUrl", "http://localhost:3001")
    .WithEnvironment(
        "KeycloakTokenUrl",
        "http://localhost:8080/realms/agentgateway/protocol/openid-connect/token"
    )
    .WithEnvironment("GatewayApiKey", "sk-alice-abc123def456")
    .WaitFor(supportAgent);

await builder.Build().RunAsync();
