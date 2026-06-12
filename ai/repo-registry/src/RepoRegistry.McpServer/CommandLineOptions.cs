namespace RepoRegistry.McpServer;

internal enum ServerCommand
{
    InvokeTool,
    Describe,
    ServeStdio,
}

internal sealed record CommandLineOptions(
    string RegistryPath,
    ServerCommand Command,
    string? ToolName,
    IReadOnlyList<string> ToolArguments
)
{
    public static CommandLineOptions Parse(string[] args)
    {
        var values = new List<string>(args);
        string? registryPath = null;

        for (var index = 0; index < values.Count; )
        {
            if (values[index] == "--registry" && index + 1 < values.Count)
            {
                registryPath = values[index + 1];
                values.RemoveAt(index + 1);
                values.RemoveAt(index);
                continue;
            }

            index++;
        }

        if (string.IsNullOrWhiteSpace(registryPath))
        {
            throw new InvalidOperationException("Missing --registry <path>");
        }

        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "Missing command. Use describe, serve-stdio, or --tool <name>."
            );
        }

        if (values[0] == "describe")
        {
            return new CommandLineOptions(registryPath, ServerCommand.Describe, null, []);
        }

        if (values[0] == "serve-stdio")
        {
            return new CommandLineOptions(registryPath, ServerCommand.ServeStdio, null, []);
        }

        if (values[0] == "--tool" && values.Count >= 2)
        {
            return new CommandLineOptions(
                registryPath,
                ServerCommand.InvokeTool,
                values[1],
                values.Skip(2).ToArray()
            );
        }

        throw new InvalidOperationException(
            "Unknown command. Use describe, serve-stdio, or --tool <name>."
        );
    }
}
