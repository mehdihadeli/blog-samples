using System.IO.Enumeration;
using System.Text.Json;

namespace RepoRegistry.McpServer;

internal sealed class RepoRegistryService
{
    private readonly RegistryDocument document;
    private readonly IRepositoryStateReader repositoryStateReader;

    private RepoRegistryService(
        RegistryDocument document,
        IRepositoryStateReader repositoryStateReader
    )
    {
        this.document = document;
        this.repositoryStateReader = repositoryStateReader;
    }

    public static RepoRegistryService Load(
        string registryPath,
        IRepositoryStateReader repositoryStateReader
    )
    {
        var json = File.ReadAllText(registryPath);
        var document =
            JsonSerializer.Deserialize<RegistryDocument>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? throw new InvalidOperationException("Failed to parse registry file.");

        return new RepoRegistryService(document, repositoryStateReader);
    }

    public ListReposResponse ListRepos() =>
        new(document.Repos.Count, document.Repos.Select(CreateSummary).ToArray());

    public WorkspaceStatusResponse GetWorkspaceStatus() =>
        new(document.Repos.Select(CreateSummary).ToArray());

    public RepoInfoResponse GetRepoInfo(string name)
    {
        var repo = FindRepo(name);
        return new RepoInfoResponse(
            repo.Name,
            repo.Path,
            repo.Stack,
            repo.DependsOn.ToArray(),
            repo.Dependents.ToArray(),
            repositoryStateReader.Read(repo)
        );
    }

    public DependentsResponse FindDependents(string name)
    {
        var repo = FindRepo(name);
        var allDependents = new List<string>();
        TraverseDependents(
            repo.Name,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            allDependents
        );

        return new DependentsResponse(
            repo.Name,
            repo.Dependents.ToArray(),
            allDependents.ToArray()
        );
    }

    public SearchAcrossReposResponse SearchAcrossRepos(string pattern, string? filePattern = null)
    {
        var matches = new List<SearchMatch>();

        foreach (var repo in document.Repos)
        {
            if (!Directory.Exists(repo.Path))
            {
                continue;
            }

            foreach (
                var file in Directory.EnumerateFiles(repo.Path, "*", SearchOption.AllDirectories)
            )
            {
                if (
                    file.Contains(
                        Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                if (
                    !string.IsNullOrWhiteSpace(filePattern)
                    && !FileSystemName.MatchesSimpleExpression(
                        filePattern,
                        Path.GetFileName(file),
                        ignoreCase: true
                    )
                )
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (var index = 0; index < lines.Length; index++)
                {
                    if (lines[index].Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(
                            new SearchMatch(repo.Name, file, index + 1, lines[index].Trim())
                        );
                    }
                }
            }
        }

        return new SearchAcrossReposResponse(pattern, filePattern, matches);
    }

    private RepoSummary CreateSummary(RepoDefinition repo) =>
        new(repo.Name, repo.Stack, repositoryStateReader.Read(repo));

    private RepoDefinition FindRepo(string name) =>
        document.Repos.FirstOrDefault(repo =>
            string.Equals(repo.Name, name, StringComparison.OrdinalIgnoreCase)
        ) ?? throw new InvalidOperationException($"Repo not found: {name}");

    private void TraverseDependents(string name, ISet<string> visited, ICollection<string> results)
    {
        if (!visited.Add(name))
        {
            return;
        }

        var repo = FindRepo(name);
        foreach (var dependent in repo.Dependents)
        {
            if (!results.Contains(dependent, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(dependent);
            }

            TraverseDependents(dependent, visited, results);
        }
    }
}
