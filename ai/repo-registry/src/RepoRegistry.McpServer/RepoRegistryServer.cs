using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RepoRegistry.McpServer;

internal sealed class RepoRegistryServer
{
    private readonly string serverName;
    private readonly RepoRegistryTools tools;

    public RepoRegistryServer(string serverName, RepoRegistryTools tools)
    {
        this.serverName = serverName;
        this.tools = tools;
    }

    public ServerDescription Describe() =>
        new(
            serverName,
            "stdio",
            "This sample uses the official MCP stdio host and exposes typed tool functions over a thin server boundary.",
            tools.GetToolNames()
        );

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder
            .Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithTools(tools.CreateMcpServerTools());

        return builder.Build().RunAsync(cancellationToken);
    }

    public object InvokeForCli(string toolName, IReadOnlyList<string> toolArguments) =>
        tools.InvokeForCli(toolName, toolArguments);
}
