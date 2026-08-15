using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace McpGateway.Tests;

internal enum AuthMode
{
    XApiKey,
    Bearer,
}

internal sealed class McpSession(string id, AuthMode auth)
{
    public string Id { get; } = id;
    public AuthMode Auth { get; } = auth;
}

internal sealed class McpException(string message) : Exception(message);

internal sealed record PostResult(
    HttpStatusCode Status,
    HttpResponseHeaders Headers,
    string Body,
    string ContentType
);

/// <summary>
/// Minimal MCP Streamable HTTP client — the .NET counterpart of the old
/// scripts/verify.mjs. Speaks JSON-RPC 2.0 over HTTP:
///   initialize -> notifications/initialized -> tools/list -> tools/call.
/// Auth: x-api-key header first, falls back to Authorization: Bearer.
/// Responses may be JSON or SSE (text/event-stream); both are handled.
/// </summary>
internal sealed class McpGatewayClient(string baseUrl, string apiKey) : IDisposable
{
    private const string ProtocolVersion = "2025-06-18";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly string _baseUrl = baseUrl.TrimEnd('/');
    private readonly string _apiKey = apiKey;
    private int _seq = 10;

    public string BaseUrl => _baseUrl;

    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Reachability probe: an unauthenticated GET must produce an HTTP
    /// response (401/403) — any response means the gateway is up.
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync(_baseUrl + "/memory", ct);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<McpSession> OpenSessionAsync(string path, CancellationToken ct = default)
    {
        object init = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "verify-agentgateway", version = "1.0.0" },
            },
        };

        var auth = AuthMode.XApiKey;
        var post = await PostAsync(path, init, auth, null, ct);
        if (post.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            auth = AuthMode.Bearer;
            post = await PostAsync(path, init, auth, null, ct);
        }

        if (post.Status != HttpStatusCode.OK)
        {
            throw new McpException(
                $"initialize {path} -> HTTP {(int)post.Status}: {post.Body[..Math.Min(300, post.Body.Length)]}"
            );
        }

        var sessionId = post.Headers.GetValues("mcp-session-id").FirstOrDefault();
        if (string.IsNullOrEmpty(sessionId))
        {
            throw new McpException($"initialize {path}: no Mcp-Session-Id header");
        }

        // notifications/initialized (id-less notification, required by spec)
        await PostAsync(
            path,
            new { jsonrpc = "2.0", method = "notifications/initialized" },
            auth,
            sessionId,
            ct
        );

        return new McpSession(sessionId, auth);
    }

    public async Task<JsonElement> RpcAsync(
        string path,
        McpSession session,
        string method,
        object? prms = null,
        CancellationToken ct = default
    )
    {
        var id = Interlocked.Increment(ref _seq);
        object body = prms is null
            ? new
            {
                jsonrpc = "2.0",
                id,
                method,
            }
            : new
            {
                jsonrpc = "2.0",
                id,
                method,
                @params = prms,
            };

        var post = await PostAsync(path, body, session.Auth, session.Id, ct);
        var final = ParseBody(post.Body, post.ContentType);

        if (final.TryGetProperty("error", out var err))
        {
            throw new McpException($"{method} {path} -> HTTP {(int)post.Status}: {err}");
        }

        if (!final.TryGetProperty("result", out var result))
        {
            throw new McpException($"{method} {path}: no result field in response");
        }

        return result;
    }

    private async Task<PostResult> PostAsync(
        string path,
        object payload,
        AuthMode auth,
        string? sessionId,
        CancellationToken ct
    )
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path);
        req.Headers.Accept.ParseAdd("application/json, text/event-stream");
        if (auth == AuthMode.XApiKey)
        {
            req.Headers.Add("x-api-key", _apiKey);
        }
        else
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        if (!string.IsNullOrEmpty(sessionId))
        {
            req.Headers.Add("mcp-session-id", sessionId);
        }

        req.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        var contentType = res.Content.Headers.ContentType?.MediaType ?? string.Empty;
        return new PostResult(res.StatusCode, res.Headers, body, contentType);
    }

    private static JsonElement ParseBody(string text, string contentType)
    {
        if (contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            JsonElement? last = null;
            foreach (var line in text.Split('\n'))
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                var data = line["data:".Length..].Trim();
                if (data.Length == 0 || data == "[DONE]")
                {
                    continue;
                }

                last = JsonSerializer.Deserialize<JsonElement>(data);
            }

            return last ?? throw new McpException($"empty SSE response: {text}");
        }

        return JsonSerializer.Deserialize<JsonElement>(text);
    }
}
