using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace AgentGateway.Samples.Tests;

public sealed class AdminUiTests : GatewayTestBase
{
    public AdminUiTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task Admin_ui_is_served()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.AdminUrl, "AgentGateway Admin UI");

        using var client = new HttpClient();
        var response = await client.GetAsync($"{Settings.Gateway.AdminUrl.TrimEnd('/')}/ui/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("AgentGateway", Case.Insensitive);
    }

    [Fact]
    public async Task Metrics_endpoint_exposes_gateway_metrics()
    {
        await SkipIfNotReachableAsync(Settings.Gateway.MetricsUrl, "Gateway metrics");

        using var client = new HttpClient();
        var response = await client.GetAsync(Settings.Gateway.MetricsUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("agentgateway");
    }
}
