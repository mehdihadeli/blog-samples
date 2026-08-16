using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;

// ---------------------------------------------------------------------------
// SupportChat
//
// A plain console client that talks to DeepSeek, to MCP tools, and to an A2A
// agent EXCLUSIVELY through AgentGateway. It never touches a backend directly:
//   - LLM calls     -> http://localhost:4000/v1   (gateway LLM port)
//   - MCP tools     -> http://localhost:3000/mcp  (gateway MCP port)
//   - A2A agent     -> http://localhost:3001      (gateway A2A route)
//
// The MCP calls are authenticated with a Keycloak access token (the gateway
// is configured with mcpAuthentication: keycloak), and the LLM calls carry a
// virtual API key created with llm.policies.apiKey.
// ---------------------------------------------------------------------------

const string GatewayMcpUrl = "http://localhost:3000/mcp";
const string GatewayLlmUrl = "http://localhost:4000/v1";
const string GatewayA2AUrl = "http://localhost:3001";
const string KeycloakTokenUrl =
    "http://localhost:8080/realms/agentgateway/protocol/openid-connect/token";
const string GatewayApiKey = "sk-alice-abc123def456"; // virtual key, metadata.user = "alice"

// 1) Get a Keycloak access token (password grant, client "support-chat").
//    The client is confidential, so the request also carries a client secret.
using var http = new HttpClient();
var form = new FormUrlEncodedContent(
    new Dictionary<string, string>
    {
        ["grant_type"] = "password",
        ["client_id"] = "support-chat",
        ["client_secret"] = "support-chat-secret",
        ["username"] = "alice",
        ["password"] = "alice-password",
    }
);
var tokenResponse = await http.PostAsync(KeycloakTokenUrl, form);
tokenResponse.EnsureSuccessStatusCode();
var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
var accessToken = tokenJson.GetProperty("access_token").GetString()!;
Console.WriteLine(
    $"[keycloak] obtained access token for alice ({(await GetTokenClaimsAsync(accessToken)).GetProperty("sub").GetString()})"
);

// 2) Connect to the MCP endpoint exposed by the gateway. The virtual MCP
//    multiplexes tickets, catalog, customers, everything, and time tools.
using var loggerFactory = LoggerFactory.Create(b =>
    b.AddConsole().SetMinimumLevel(LogLevel.Warning)
);
var transport = new HttpClientTransport(
    new HttpClientTransportOptions
    {
        Endpoint = new Uri(GatewayMcpUrl),
        AdditionalHeaders = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {accessToken}",
        },
    },
    loggerFactory
);

await using var mcpClient = await McpClient.CreateAsync(
    transport,
    new McpClientOptions
    {
        ClientInfo = new Implementation { Name = "support-chat", Version = "1.0.0" },
    },
    loggerFactory
);

var tools = await mcpClient.ListToolsAsync();
Console.WriteLine(
    $"[mcp] gateway exposed {tools.Count} tools (multiplexed + authorization filtered):"
);
foreach (var tool in tools)
{
    Console.WriteLine($"      - {tool.Name}");
}

// 3) LLM client: DeepSeek through the gateway, using the virtual key.
var openAiOptions = new OpenAIClientOptions
{
    Transport = new HttpClientPipelineTransport(
        new HttpClient { BaseAddress = new Uri(GatewayLlmUrl + "/") }
    ),
};
var openAiClient = new OpenAIClient(new ApiKeyCredential(GatewayApiKey), openAiOptions);

// "deepseek-smart" is the gateway's weighted virtual model.
var chatClient = openAiClient.GetChatClient("deepseek-smart").AsIChatClient();

// Make the MCP tools callable from the chat pipeline (Microsoft.Extensions.AI).
chatClient = chatClient
    .AsBuilder()
    .UseFunctionInvocation(
        null,
        options =>
        {
            foreach (var tool in tools)
            {
                options!.AdditionalTools.Add(tool);
            }
        }
    )
    .Build();

// 4) Demo turns. Each one crosses the gateway (LLM + MCP tool calls inside).
await RunTurnAsync("List the open support tickets.", chatClient);
await RunTurnAsync("Search the catalog for a laptop and check its stock.", chatClient);
await RunTurnAsync("Create a ticket for the login outage.", chatClient);

// 5) Talk to the A2A agent through the gateway's A2A route.
Console.WriteLine();
Console.WriteLine("[a2a] sending message/send to the agent through the gateway...");
var agentReply = await SendA2AMessageAsync(
    GatewayA2AUrl,
    "Please summarize the current open tickets."
);
Console.WriteLine($"[a2a] agent reply: {agentReply}");

static async Task RunTurnAsync(string prompt, IChatClient chatClient)
{
    Console.WriteLine();
    Console.WriteLine($">>> {prompt}");
    var response = await chatClient.GetResponseAsync(prompt);
    Console.WriteLine(response.Text);
}

static async Task<JsonElement> GetTokenClaimsAsync(string accessToken)
{
    // Just decode the JWT payload for display purposes.
    var parts = accessToken.Split('.');
    var payload = Base64UrlDecode(parts[1]);
    return JsonDocument.Parse(payload).RootElement;
}

static string Base64UrlDecode(string input)
{
    var padded = input.Replace('-', '+').Replace('_', '/');
    padded += new string('=', (4 - padded.Length % 4) % 4);
    return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
}

static async Task<string?> SendA2AMessageAsync(string agentEndpoint, string text)
{
    using var client = new HttpClient();
    var requestId = Guid.NewGuid().ToString("N");
    var payload = new
    {
        jsonrpc = "2.0",
        id = requestId,
        method = "message/send",
        @params = new { message = new { role = "user", parts = new[] { new { text } } } },
    };

    var response = await client.PostAsJsonAsync(agentEndpoint, payload);
    response.EnsureSuccessStatusCode();
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

    // Walk: result.task.artifacts[*].parts[*].text
    if (
        json.TryGetProperty("result", out var result)
        && result.TryGetProperty("task", out var task)
        && task.TryGetProperty("artifacts", out var artifacts)
    )
    {
        var texts = artifacts
            .EnumerateArray()
            .SelectMany(a =>
                a.TryGetProperty("parts", out var parts)
                    ? parts
                        .EnumerateArray()
                        .Where(p => p.TryGetProperty("text", out _))
                        .Select(p => p.GetProperty("text").GetString() ?? string.Empty)
                    : []
            )
            .Where(t => t.Length > 0);
        return string.Join("\n", texts);
    }

    return json.GetRawText();
}
