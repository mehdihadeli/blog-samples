using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace RepoRegistry.McpServer;

internal sealed class RepoRegistryTools
{
    private readonly RepoRegistryService registryService;

    private static readonly string[] ToolNames =
    [
        "list_repos",
        "get_workspace_status",
        "get_repo_info",
        "find_dependents",
        "search_across_repos",
    ];

    public RepoRegistryTools(RepoRegistryService registryService)
    {
        this.registryService = registryService;
    }

    public ListReposResponse ListRepos() => registryService.ListRepos();

    public WorkspaceStatusResponse GetWorkspaceStatus() => registryService.GetWorkspaceStatus();

    public RepoInfoResponse GetRepoInfo(string repo) => registryService.GetRepoInfo(repo);

    public DependentsResponse FindDependents(string repo) => registryService.FindDependents(repo);

    public SearchAcrossReposResponse SearchAcrossRepos(
        string pattern,
        string? filePattern = null
    ) => registryService.SearchAcrossRepos(pattern, filePattern);

    public IReadOnlyList<string> GetToolNames() => ToolNames;

    public IReadOnlyList<McpServerTool> CreateMcpServerTools() =>
        [
            McpServerTool.Create(
                AIFunctionFactory.Create(
                    ListRepos,
                    new AIFunctionFactoryOptions
                    {
                        Name = "list_repos",
                        Description =
                            "List the repositories known to the workspace registry, including their stack and live Git state.",
                    }
                )
            ),
            McpServerTool.Create(
                AIFunctionFactory.Create(
                    GetWorkspaceStatus,
                    new AIFunctionFactoryOptions
                    {
                        Name = "get_workspace_status",
                        Description = "Get live Git-backed status for all registered repositories.",
                    }
                )
            ),
            McpServerTool.Create(
                AIFunctionFactory.Create(
                    GetRepoInfo,
                    new AIFunctionFactoryOptions
                    {
                        Name = "get_repo_info",
                        Description =
                            "Get registry metadata and live status for a single repository.",
                    }
                )
            ),
            McpServerTool.Create(
                AIFunctionFactory.Create(
                    FindDependents,
                    new AIFunctionFactoryOptions
                    {
                        Name = "find_dependents",
                        Description =
                            "Find the direct and recursive dependents of a registered repository.",
                    }
                )
            ),
            McpServerTool.Create(
                AIFunctionFactory.Create(
                    SearchAcrossRepos,
                    new AIFunctionFactoryOptions
                    {
                        Name = "search_across_repos",
                        Description =
                            "Search for a text pattern across all registered repositories, optionally filtered by file name pattern.",
                    }
                )
            ),
        ];

    public object InvokeForCli(string toolName, IReadOnlyList<string> toolArguments) =>
        toolName switch
        {
            "list_repos" => ListRepos(),
            "get_workspace_status" => GetWorkspaceStatus(),
            "get_repo_info" when toolArguments.Count >= 1 => GetRepoInfo(toolArguments[0]),
            "find_dependents" when toolArguments.Count >= 1 => FindDependents(toolArguments[0]),
            "search_across_repos" when toolArguments.Count >= 1 => SearchAcrossRepos(
                toolArguments[0],
                toolArguments.Count >= 2 ? toolArguments[1] : null
            ),
            _ => throw new InvalidOperationException(
                $"Unknown or incomplete tool invocation: {toolName}"
            ),
        };
}
