using System.ClientModel;
using System.ClientModel.Primitives;
using A2A;
using A2A.Models;
using A2A.Server;
using A2A.Server.Services;
using Microsoft.Extensions.AI;
using OpenAI;
using SupportAgent;

// ---------------------------------------------------------------------------
// SupportAgent
//
// A minimal A2A (Agent-to-Agent protocol) host built on the a2a-net SDK.
// AgentGateway fronts it with the `a2a` route policy, serves its agent card
// on /.well-known/agent.json, and exposes it to other agents over port 3001.
// The agent itself calls DeepSeek through the gateway (same virtual key).
// ---------------------------------------------------------------------------

var gatewayLlmUrl =
    Environment.GetEnvironmentVariable("GATEWAY_LLM_URL") ?? "http://localhost:4000/v1";
var gatewayApiKey =
    Environment.GetEnvironmentVariable("GATEWAY_API_KEY") ?? "sk-alice-abc123def456";

var builder = WebApplication.CreateBuilder(args);

// DeepSeek through the gateway, OpenAI-compatible endpoint.
var openAiOptions = new OpenAIClientOptions
{
    Transport = new HttpClientPipelineTransport(
        new HttpClient { BaseAddress = new Uri(gatewayLlmUrl + "/") }
    ),
};
var openAiClient = new OpenAIClient(new ApiKeyCredential(gatewayApiKey), openAiOptions);
builder.Services.AddSingleton<IChatClient>(_ =>
    // "deepseek-smart" is the gateway's weighted virtual model.
    openAiClient.GetChatClient("deepseek-smart").AsIChatClient()
);

// A2A server: one hosted agent with a card, in-memory store + task queue,
// HTTP (JSON-RPC) transport.
builder.Services.AddA2AServer(server =>
    server
        .SupportsStreaming()
        .Host(agent =>
            agent
                .WithCard(card =>
                    card.WithName("Support Escalation Agent")
                        .WithDescription(
                            "A2A agent that summarizes support tickets and answers escalation questions."
                        )
                        .WithVersion("1.0.0")
                        .WithSkill(skill =>
                            skill
                                .WithName("Chat")
                                .WithDescription("Chat with the support escalation agent.")
                                .WithTag("chat")
                        )
                )
                .UseRuntime<SupportAgentRuntime>()
        )
        .UseMemoryStore()
        .UseMemoryTaskQueue()
        .UseHttpTransport()
);

var app = builder.Build();

app.UseA2AServer();

app.Run();
