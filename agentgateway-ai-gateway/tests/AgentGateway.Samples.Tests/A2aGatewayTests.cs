using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace AgentGateway.Samples.Tests;

public sealed class A2aGatewayTests : GatewayTestBase
{
    public A2aGatewayTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task A2a_route_requires_bearer_token()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.A2AUrl, "A2A gateway");

        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(
            $"{Settings.Gateway.A2AUrl.TrimEnd('/')}/v1/message:send",
            BuildA2AMessage("hello")
        );

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A2a_message_send_accepts_valid_keycloak_token()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.A2AUrl, "A2A gateway");
        await SkipIfNotReachableAsync(Settings.Gateway.LlmUrl, "LLM gateway (A2A agent needs it)");

        var token = await GetKeycloakTokenAsync(
            Settings.Users["Alice"].Username,
            Settings.Users["Alice"].Password
        );
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            Settings.Gateway.A2AUrl,
            BuildA2AMessage("Please summarize the current open tickets.")
        );

        // The gateway must accept the JWT. The upstream agent needs DeepSeek
        // to actually complete the task, so any 5xx from upstream is recorded
        // but does not fail the auth assertion.
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);

        Output.WriteLine($"A2A status: {response.StatusCode}");
        Output.WriteLine(await response.Content.ReadAsStringAsync());
    }

    private static object BuildA2AMessage(string text)
    {
        return new
        {
            message = new
            {
                messageId = Guid.NewGuid().ToString("N"),
                role = "user",
                parts = new[] { new { kind = "text", text } },
            },
        };
    }
}
