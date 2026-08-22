using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;
using Xunit.Sdk;

namespace AgentGateway.Samples.Tests;

public abstract class GatewayTestBase : IAsyncLifetime
{
    private readonly HttpClient _httpClient;
    protected GatewaySettings Settings { get; }
    protected ITestOutputHelper Output { get; }

    protected GatewayTestBase(ITestOutputHelper output)
    {
        Output = output;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("testsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        Settings = configuration.Get<GatewaySettings>() ?? new GatewaySettings();
        _httpClient = new HttpClient();
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public virtual ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        return ValueTask.CompletedTask;
    }

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    protected HttpClient HttpClient => _httpClient;

    /// <summary>
    /// Skips the current test if the given endpoint is not reachable.
    /// Use this to keep the suite useful when the Docker stack is not running.
    /// </summary>
    protected async Task SkipIfNotReachableAsync(string url, string because)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            var response = await _httpClient.GetAsync(url, cts.Token);
            if ((int)response.StatusCode >= 500)
            {
                Assert.Skip($"{because} returned server error {response.StatusCode} at {url}");
            }
        }
        catch (SkipException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Assert.Skip($"{because} is not reachable at {url}: {ex.Message}");
        }
    }

    protected async Task<string> GetKeycloakTokenAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = Settings.Keycloak.ClientId,
                ["client_secret"] = Settings.Keycloak.ClientSecret,
                ["username"] = username,
                ["password"] = password,
            }
        );

        var response = await _httpClient.PostAsync(
            Settings.Keycloak.TokenUrl,
            form,
            cancellationToken
        );
        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync(cancellationToken)
        );

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return json.GetProperty("access_token").GetString()!;
    }

    protected static JsonElement ParseJwtPayload(string accessToken)
    {
        var parts = accessToken.Split('.');
        parts.Length.ShouldBe(3);

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload += new string('=', (4 - payload.Length % 4) % 4);

        var bytes = Convert.FromBase64String(payload);
        return JsonDocument.Parse(bytes).RootElement;
    }
}
