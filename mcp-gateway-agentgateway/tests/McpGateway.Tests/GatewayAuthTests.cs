using System.Net;

namespace McpGateway.Tests;

/// <summary>
/// [1] Auth rejection — a request without an API key must be rejected.
/// </summary>
[Collection("gateway")]
public class GatewayAuthTests(GatewayFixture fixture) : GatewayTestBase(fixture)
{
    [Fact]
    public async Task Get_memory_without_api_key_is_rejected()
    {
        await RequireGatewayAsync();

        using var http = new HttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, Fixture.Client.BaseUrl + "/memory");
        req.Headers.Accept.ParseAdd("application/json, text/event-stream");

        using var res = await http.SendAsync(req);

        res.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }
}
