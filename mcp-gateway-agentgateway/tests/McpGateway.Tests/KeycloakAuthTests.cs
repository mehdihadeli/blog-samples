using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace McpGateway.Tests;

/// <summary>
/// Keycloak JWT path: the gateway exposes a second entry port (:8082, the
/// `sso` gateway) where a valid Keycloak-issued Bearer JWT (realm mcp-demo,
/// client mcp-gateway) is REQUIRED — no API key accepted there.
///
/// The Keycloak user is configurable via env (KC_USER / KC_PASSWORD) so each
/// developer can test with their own account (defaults: mcpuser/mcpuser123).
///
/// Skips when Keycloak (:8081) is not reachable.
/// </summary>
[Collection("gateway")]
public class KeycloakAuthTests(GatewayFixture fixture) : GatewayTestBase(fixture)
{
    private const string KeycloakBase = "http://localhost:8081";
    private const string SsoGatewayBase = "http://localhost:8082";
    private const string Realm = "mcp-demo";

    // `mcp-gateway` is a PUBLIC PKCE client (used by the mcpAuthentication
    // DCR short-circuit + VS Code Copilot OAuth flow). Keycloak still allows
    // the password grant (directAccessGrantsEnabled), just without a secret.
    private const string ClientId = "mcp-gateway";

    private static string KcUser => Environment.GetEnvironmentVariable("KC_USER") ?? "mcpuser";

    private static string KcPassword =>
        Environment.GetEnvironmentVariable("KC_PASSWORD") ?? "mcpuser123";

    private async Task<string?> TryGetTokenAsync()
    {
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = ClientId,
                ["username"] = KcUser,
                ["password"] = KcPassword,
            }
        );

        using var http = new HttpClient();
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"{KeycloakBase}/realms/{Realm}/protocol/openid-connect/token"
        )
        {
            Content = form,
        };

        try
        {
            using var res = await http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                return null;
            }

            var body = JsonSerializer.Deserialize<JsonElement>(
                await res.Content.ReadAsStringAsync()
            );
            return body.TryGetProperty("access_token", out var tok) ? tok.GetString() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    [Fact]
    public async Task Keycloak_bearer_token_authenticates_on_sso_port()
    {
        await RequireGatewayAsync();

        var token = await TryGetTokenAsync();
        if (token is null)
        {
            Assert.Skip(
                "Keycloak not reachable or token request failed — start the stack (scripts/start-toolhive.sh)"
            );
        }

        using var http = new HttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, SsoGatewayBase + "/memory");
        req.Headers.Accept.ParseAdd("application/json, text/event-stream");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(
            JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "initialize",
                    @params = new
                    {
                        protocolVersion = "2025-06-18",
                        capabilities = new { },
                        clientInfo = new { name = "verify-keycloak", version = "1.0.0" },
                    },
                }
            ),
            Encoding.UTF8,
            "application/json"
        );

        using var res = await http.SendAsync(req);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
