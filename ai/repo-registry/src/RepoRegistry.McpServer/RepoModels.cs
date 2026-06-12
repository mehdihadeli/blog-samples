namespace RepoRegistry.McpServer;

internal sealed class RegistryDocument
{
    public List<RepoDefinition> Repos { get; set; } = [];
}

internal sealed class RepoDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Stack { get; set; } = string.Empty;
    public List<string> DependsOn { get; set; } = [];
    public List<string> Dependents { get; set; } = [];
}

internal sealed record RepositoryState(
    bool Exists,
    string Path,
    string CurrentBranch,
    string Commit,
    bool HasUncommitted,
    int UncommittedCount
);

internal sealed record RepoSummary(string Name, string Stack, RepositoryState Live);

internal sealed record ListReposResponse(int Count, IReadOnlyList<RepoSummary> Repos);

internal sealed record WorkspaceStatusResponse(IReadOnlyList<RepoSummary> Repos);

internal sealed record RepoInfoResponse(
    string Name,
    string Path,
    string Stack,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> Dependents,
    RepositoryState Live
);

internal sealed record DependentsResponse(
    string Repo,
    IReadOnlyList<string> DirectDependents,
    IReadOnlyList<string> AllDependents
);

internal sealed record SearchMatch(string Repo, string File, int Line, string Text);

internal sealed record SearchAcrossReposResponse(
    string Pattern,
    string? FilePattern,
    IReadOnlyList<SearchMatch> Matches
);

internal sealed record ServerDescription(
    string ServerName,
    string Transport,
    string Note,
    IReadOnlyList<string> Tools
);
