using System.Text.Json;

namespace ResearchAgent.Sample;

internal sealed class SampleDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private static readonly IReadOnlyList<ExistingPost> DefaultExistingPosts =
    [
        new(
            "agent-framework-web-search-tool-dotnet",
            "Using the Agent Framework Web Search Tool in .NET",
            ["agent framework", "web search", ".net", "tool", "citations"]
        ),
        new(
            "repo-registry-mcp-server",
            "Repo Registry MCP Server for Multi-Repo Agent Workflows",
            ["mcp", "tooling", "service discovery", "workflow"]
        ),
    ];

    private static readonly IReadOnlyList<SearchDocument> DefaultSearchDocuments =
    [
        new(
            "Microsoft Agent Framework overview",
            "https://learn.microsoft.com/agent-framework/overview/",
            "Use agents for open-ended tasks and workflows for explicit multi-step orchestration with checkpointing and human-in-the-loop support.",
            ["microsoft agent framework", ".net", "workflow", "checkpointing", "human-in-the-loop"],
            980,
            4.8
        ),
        new(
            "Web Search tool for Agent Framework",
            "https://learn.microsoft.com/agent-framework/agents/tools/web-search",
            "Give agents access to fresh web information while keeping security, privacy, and provider support in mind.",
            ["web search", ".net", "tool", "citations", "security"],
            910,
            4.7
        ),
        new(
            ".NET AI ecosystem tools and SDKs",
            "https://learn.microsoft.com/dotnet/ai/dotnet-ai-ecosystem",
            "Explains where Microsoft Agent Framework fits relative to Microsoft.Extensions.AI and other .NET AI building blocks.",
            [".net", "ai", "microsoft.extensions.ai", "agent framework"],
            760,
            4.5
        ),
        new(
            "Security and privacy considerations for web search tools",
            "https://learn.microsoft.com/azure/foundry/agents/how-to/tools/web-search#security-and-privacy-considerations",
            "Treat web results as untrusted input, avoid sending secrets, and plan for result variability and rate limits.",
            ["security", "privacy", "web search", "rate limits", "citations"],
            870,
            4.7
        ),
    ];

    private readonly string _baseDirectory;

    public SampleDataLoader(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    public async Task<IReadOnlyList<ExistingPost>> LoadExistingPostsAsync(
        string? path,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return DefaultExistingPosts;
        }

        var resolvedPath = ResolvePath(path, "existing-posts.json");
        await using var stream = File.OpenRead(resolvedPath);
        var posts = await JsonSerializer.DeserializeAsync<List<ExistingPost>>(
            stream,
            JsonOptions,
            cancellationToken: cancellationToken
        );
        return posts ?? [];
    }

    public async Task<IReadOnlyList<SearchDocument>> LoadSearchDocumentsAsync(
        string? path,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return DefaultSearchDocuments;
        }

        var resolvedPath = ResolvePath(path, "search-index.json");
        await using var stream = File.OpenRead(resolvedPath);
        var documents = await JsonSerializer.DeserializeAsync<List<SearchDocument>>(
            stream,
            JsonOptions,
            cancellationToken: cancellationToken
        );
        return documents ?? [];
    }

    private string ResolvePath(string? overridePath, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        throw new ArgumentException($"A path is required for {fileName}.", nameof(overridePath));
    }
}
