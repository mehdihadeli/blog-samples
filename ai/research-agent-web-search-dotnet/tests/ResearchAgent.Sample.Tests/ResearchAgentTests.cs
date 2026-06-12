using System.Reflection;
using Xunit;

namespace ResearchAgent.Sample.Tests;

public sealed class ResearchAgentTests
{
    [Fact]
    public async Task SampleDataLoader_LoadsSeedData_WithExpectedCollections()
    {
        var loader = new SampleDataLoader(GetOutputDirectory());

        var posts = await loader.LoadExistingPostsAsync(path: null, CancellationToken.None);
        var documents = await loader.LoadSearchDocumentsAsync(path: null, CancellationToken.None);

        Assert.NotEmpty(posts);
        Assert.NotEmpty(documents);
        Assert.All(posts, post => Assert.NotEmpty(post.Keywords));
        Assert.All(documents, document => Assert.NotEmpty(document.Tags));
        Assert.All(documents, document => Assert.True(document.PopularityScore > 0));
        Assert.All(documents, document => Assert.InRange(document.CommunityRating, 1.0, 5.0));
    }

    [Fact]
    public async Task CreateBriefAsync_ReturnsStructuredBrief_WithOverlapAndBoundedQueries()
    {
        var loader = new SampleDataLoader(GetOutputDirectory());
        var posts = await loader.LoadExistingPostsAsync(path: null, CancellationToken.None);
        var documents = await loader.LoadSearchDocumentsAsync(path: null, CancellationToken.None);

        var agent = new ResearchAgent(posts, new FakeWebSearchClient(documents));

        var brief = await agent.CreateBriefAsync(
            "research agent web search in .NET",
            maxIterations: 3,
            CancellationToken.None
        );

        Assert.Equal("research agent web search in .NET", brief.Topic);
        Assert.Contains("agent-framework-web-search-tool-dotnet", brief.ExistingCoverage);
        Assert.Equal(3, brief.SearchQueries.Count);
        Assert.NotEmpty(brief.Sources);
        Assert.NotEmpty(brief.Outline);
        Assert.All(brief.Sources, source => Assert.True(source.PopularityScore > 0));
        Assert.All(brief.Sources, source => Assert.InRange(source.CommunityRating, 1.0, 5.0));
        Assert.Contains(
            brief.Risks,
            risk => risk.Contains("untrusted input", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public async Task SearchAsync_PrefersMorePopularAndHigherRatedMatch_WhenTextRelevanceIsSimilar()
    {
        var documents = new[]
        {
            new SearchDocument(
                "Structured Research Agent Guide",
                "https://example.com/low-signal",
                "Research agent web search in .NET with structured briefs.",
                ["research agent", "web search", ".net"],
                120,
                3.9
            ),
            new SearchDocument(
                "Structured Research Agent Guide",
                "https://example.com/high-signal",
                "Research agent web search in .NET with structured briefs.",
                ["research agent", "web search", ".net"],
                980,
                4.9
            ),
        };

        var client = new FakeWebSearchClient(documents);
        var results = await client.SearchAsync(
            "research agent web search in .NET",
            CancellationToken.None
        );

        Assert.Equal("https://example.com/high-signal", results[0].Url);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public async Task RunAsync_WithoutTopic_ReturnsUsageError()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        var exitCode = await ProgramEntry.RunAsync(
            [],
            standardOutput,
            standardError,
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.Contains(
            "Usage: --topic <value>",
            standardError.ToString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task RunAsync_WithTopic_WritesStructuredJsonToStdout()
    {
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        var exitCode = await ProgramEntry.RunAsync(
            ["--topic", "research agent web search in .NET"],
            standardOutput,
            standardError,
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, standardError.ToString());
        Assert.Contains(
            "\"Topic\": \"research agent web search in .NET\"",
            standardOutput.ToString(),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "\"ExistingCoverage\"",
            standardOutput.ToString(),
            StringComparison.Ordinal
        );
        Assert.Contains("\"Sources\"", standardOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"PopularityScore\"", standardOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"CommunityRating\"", standardOutput.ToString(), StringComparison.Ordinal);
    }

    private static string GetOutputDirectory()
    {
        return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Could not resolve test output directory.");
    }
}
