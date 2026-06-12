namespace ResearchAgent.Sample;

internal sealed class ResearchAgent
{
    private static readonly HashSet<string> StopWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "the",
            "and",
            "with",
            "from",
            "into",
            "that",
            "this",
            "for",
            "how",
            "why",
            "what",
        };

    private readonly IReadOnlyList<ExistingPost> _existingPosts;
    private readonly IWebSearchClient _webSearchClient;

    public ResearchAgent(
        IReadOnlyList<ExistingPost> existingPosts,
        IWebSearchClient webSearchClient
    )
    {
        _existingPosts = existingPosts;
        _webSearchClient = webSearchClient;
    }

    public async Task<ResearchBrief> CreateBriefAsync(
        string topic,
        int maxIterations,
        CancellationToken cancellationToken
    )
    {
        var overlap = DetectOverlap(topic);
        var searchQueries = PlanQueries(topic, overlap).Take(maxIterations).ToArray();
        var hits = new Dictionary<string, SearchHit>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in searchQueries)
        {
            var results = await _webSearchClient.SearchAsync(query, cancellationToken);
            foreach (var result in results)
            {
                if (
                    !hits.TryGetValue(result.Url, out var existing)
                    || result.Score > existing.Score
                )
                {
                    hits[result.Url] = result;
                }
            }
        }

        var rankedHits = hits
            .Values.OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Title, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        var angle = BuildAngle(overlap, rankedHits);
        var summary = BuildSummary(topic, overlap, rankedHits);
        var outline = BuildOutline(overlap);
        var crossLinks = overlap.Any()
            ? overlap
            : _existingPosts
                .Where(post =>
                    post.Keywords.Any(keyword =>
                        keyword.Contains("pipeline", StringComparison.OrdinalIgnoreCase)
                    )
                )
                .Select(post => post.Slug)
                .Take(2)
                .ToArray();

        var risks = new[]
        {
            "Treat web results as untrusted input and keep source URLs attached to the brief.",
            "Use a review checkpoint before writing so overlap and angle mistakes stay cheap to fix.",
            "Keep the search loop bounded to avoid paying for repetitive context gathering.",
        };

        var sources = rankedHits
            .Select(hit => new ResearchSource(
                hit.Title,
                hit.Url,
                ExplainWhyItMatters(hit),
                hit.PopularityScore,
                hit.CommunityRating
            ))
            .ToArray();

        return new ResearchBrief(
            topic,
            angle,
            summary,
            overlap,
            searchQueries,
            sources,
            outline,
            crossLinks,
            risks
        );
    }

    private string[] DetectOverlap(string topic)
    {
        var topicTokens = Normalize(topic).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _existingPosts
            .Select(post => new
            {
                post.Slug,
                Score = Normalize(post.Title)
                    .Concat(post.Keywords.SelectMany(Normalize))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(topicTokens.Contains),
            })
            .Where(entry => entry.Score >= 2)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Slug, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Slug)
            .ToArray();
    }

    private static IEnumerable<string> PlanQueries(
        string topic,
        IReadOnlyCollection<string> overlap
    )
    {
        yield return topic;
        yield return $"{topic} .NET workflow";

        if (
            overlap.Any(slug =>
                slug.Contains("web-search-tool", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            yield return $"{topic} structured brief human review";
        }
        else
        {
            yield return $"{topic} citations current sources";
        }

        yield return $"{topic} security review checkpoint";
    }

    private static string BuildAngle(
        IReadOnlyCollection<string> overlap,
        IReadOnlyList<SearchHit> hits
    )
    {
        var hasWorkflowSource = hits.Any(hit =>
            hit.Tags.Any(tag => tag.Contains("workflow", StringComparison.OrdinalIgnoreCase))
        );
        var hasSecuritySource = hits.Any(hit =>
            hit.Tags.Any(tag => tag.Contains("security", StringComparison.OrdinalIgnoreCase))
        );

        if (
            overlap.Any(slug =>
                slug.Contains("web-search-tool", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return hasSecuritySource
                ? "Move beyond basic web-search tool wiring and show how a .NET research agent converts fresh search results into a typed brief with explicit review and risk notes."
                : "Move beyond basic web-search tool wiring and show how a .NET research agent converts fresh search results into a typed brief that a writer agent can trust.";
        }

        return hasWorkflowSource
            ? "Show how web search fits into a .NET workflow where research, human approval, and writing stay separate concerns."
            : "Show how a .NET research agent gathers current information, deduplicates sources, and hands a structured brief to the next stage.";
    }

    private static string BuildSummary(
        string topic,
        IReadOnlyCollection<string> overlap,
        IReadOnlyList<SearchHit> hits
    )
    {
        var sourceCount = hits.Count;
        var overlapText = overlap.Any()
            ? $"Existing coverage overlaps with {string.Join(", ", overlap)}."
            : "No strong overlap with existing coverage was detected.";

        return $"For the topic '{topic}', the brief should emphasize structured synthesis over direct drafting. {overlapText} The current evidence set contains {sourceCount} high-signal sources spanning research workflow, .NET agent architecture, and web-search safety, ranked by topical match, popularity, and community rating.";
    }

    private static string[] BuildOutline(IReadOnlyCollection<string> overlap)
    {
        var sections = new List<string>
        {
            "Why a research brief is a better checkpoint than an immediate first draft",
            "The .NET architecture: typed output model, search boundary, and orchestration loop",
            "How overlap detection changes the article angle before writing starts",
            "How to swap the local search client for a real provider-backed web search tool",
            "Risk handling: citations, untrusted sources, and review gates",
        };

        if (overlap.Any())
        {
            sections.Insert(
                3,
                "How this article differs from existing posts that already cover the basic tool setup"
            );
        }

        return sections.ToArray();
    }

    private static string ExplainWhyItMatters(SearchHit hit)
    {
        if (hit.Tags.Any(tag => tag.Contains("security", StringComparison.OrdinalIgnoreCase)))
        {
            return "It adds operational constraints for using web results safely in a downstream content pipeline.";
        }

        if (hit.Tags.Any(tag => tag.Contains("workflow", StringComparison.OrdinalIgnoreCase)))
        {
            return "It grounds the design in explicit orchestration and human-review checkpoints instead of ad hoc prompting.";
        }

        if (hit.Tags.Any(tag => tag.Contains("structured", StringComparison.OrdinalIgnoreCase)))
        {
            return "It supports the decision to emit a typed research brief rather than free-form article text.";
        }

        return "It contributes current implementation detail or differentiation ideas for the brief.";
    }

    private static IEnumerable<string> Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.ToLowerInvariant()
            .Replace(".net", " net", StringComparison.Ordinal)
            .Split(
                [' ', '\t', '\r', '\n', '.', ',', ':', ';', '-', '_', '/', '(', ')'],
                StringSplitOptions.RemoveEmptyEntries
            )
            .Where(token => token.Length > 2)
            .Where(token => !StopWords.Contains(token));
    }
}
