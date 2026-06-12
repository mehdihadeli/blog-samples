namespace ResearchAgent.Sample;

public sealed record ExistingPost(string Slug, string Title, IReadOnlyList<string> Keywords);

public sealed record SearchDocument(
    string Title,
    string Url,
    string Summary,
    IReadOnlyList<string> Tags,
    int PopularityScore,
    double CommunityRating
);

public sealed record SearchHit(
    string Query,
    string Title,
    string Url,
    string Summary,
    double Score,
    IReadOnlyList<string> Tags,
    int PopularityScore,
    double CommunityRating
);

public sealed record ResearchSource(
    string Title,
    string Url,
    string WhyItMatters,
    int PopularityScore,
    double CommunityRating
);

public sealed record ResearchBrief(
    string Topic,
    string Angle,
    string Summary,
    IReadOnlyList<string> ExistingCoverage,
    IReadOnlyList<string> SearchQueries,
    IReadOnlyList<ResearchSource> Sources,
    IReadOnlyList<string> Outline,
    IReadOnlyList<string> CrossLinkCandidates,
    IReadOnlyList<string> Risks
);

internal interface IWebSearchClient
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken);
}
