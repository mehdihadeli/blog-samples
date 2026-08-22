namespace AgentGateway.Samples.Tests;

public sealed class GatewaySettings
{
    public GatewayEndpoints Gateway { get; set; } = new();
    public KeycloakSettings Keycloak { get; set; } = new();
    public Dictionary<string, UserSettings> Users { get; set; } = new();
    public Dictionary<string, string> ApiKeys { get; set; } = new();
}

public sealed class GatewayEndpoints
{
    public string McpUrl { get; set; } = "http://localhost:3000/mcp";
    public string LlmUrl { get; set; } = "http://localhost:4000/v1";
    public string A2AUrl { get; set; } = "http://localhost:3001";
    public string AdminUrl { get; set; } = "http://localhost:15000";
    public string MetricsUrl { get; set; } = "http://localhost:15020/metrics";
}

public sealed class KeycloakSettings
{
    public string TokenUrl { get; set; } =
        "http://localhost:8080/realms/agentgateway/protocol/openid-connect/token";
    public string ClientId { get; set; } = "support-chat";
    public string ClientSecret { get; set; } = "support-chat-secret";
    public string BrowserClientId { get; set; } = "agentgateway-browser";
}

public sealed class UserSettings
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}
