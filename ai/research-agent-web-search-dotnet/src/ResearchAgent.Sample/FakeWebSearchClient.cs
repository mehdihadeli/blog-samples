namespace ResearchAgent.Sample;

internal sealed class FakeWebSearchClient : IWebSearchClient
{
    private readonly IReadOnlyList<SearchDocument> _documents;

    public FakeWebSearchClient(IReadOnlyList<SearchDocument> documents)
    {
        _documents = documents;
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        var queryTokens = Tokenize(query).ToArray();
        var hits = _documents
            .Select(document =>
            {
                var score = Score(document, queryTokens);
                return new SearchHit(
                    query,
                    document.Title,
                    document.Url,
                    document.Summary,
                    score,
                    document.Tags,
                    document.PopularityScore,
                    document.CommunityRating
                );
            })
            .Where(hit => hit.Score > 0)
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Title, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        return Task.FromResult<IReadOnlyList<SearchHit>>(hits);
    }

    private static double Score(SearchDocument document, IReadOnlyCollection<string> queryTokens)
    {
        var searchable = string.Join(
            ' ',
            new[] { document.Title, document.Summary }.Concat(document.Tags)
        );
        var docTokens = Tokenize(searchable).ToHashSet(StringComparer.OrdinalIgnoreCase);
        double textRelevance = 0;

        foreach (var token in queryTokens)
        {
            if (docTokens.Contains(token))
            {
                textRelevance += 1;
            }
        }

        if (
            document.Tags.Any(tag => tag.Contains(".net", StringComparison.OrdinalIgnoreCase))
            && queryTokens.Contains("net", StringComparer.OrdinalIgnoreCase)
        )
        {
            textRelevance += 0.5;
        }

        var popularityWeight = Math.Clamp(document.PopularityScore / 500.0, 0, 2.5);
        var ratingWeight = Math.Clamp(document.CommunityRating / 2.0, 0, 2.5);

        return textRelevance + popularityWeight + ratingWeight;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return text.ToLowerInvariant()
            .Split(
                [' ', '\t', '\r', '\n', '.', ',', ':', ';', '-', '_', '/', '(', ')'],
                StringSplitOptions.RemoveEmptyEntries
            )
            .Where(token => token.Length > 2);
    }
}
