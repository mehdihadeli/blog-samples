using System.Text.Json;

namespace RepoRegistry.McpServer;

internal static class ProgramEntry
{
    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            var parsed = CommandLineOptions.Parse(args);
            var registryService = RepoRegistryService.Load(
                parsed.RegistryPath,
                new GitRepositoryStateReader()
            );
            var tools = new RepoRegistryTools(registryService);
            var server = new RepoRegistryServer("repo-registry", tools);

            if (parsed.Command == ServerCommand.Describe)
            {
                await stdout.WriteLineAsync(
                    JsonSerializer.Serialize(
                        server.Describe(),
                        new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        }
                    )
                );
                return 0;
            }

            if (parsed.Command == ServerCommand.ServeStdio)
            {
                await server.RunAsync();
                return 0;
            }

            var result = server.InvokeForCli(parsed.ToolName!, parsed.ToolArguments);
            await stdout.WriteLineAsync(
                JsonSerializer.Serialize(
                    result,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }
                )
            );
            return 0;
        }
        catch (Exception ex)
        {
            await stderr.WriteLineAsync(ex.Message);
            return 1;
        }
    }
}
