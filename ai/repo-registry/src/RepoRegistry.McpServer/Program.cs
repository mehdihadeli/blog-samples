using RepoRegistry.McpServer;

var exitCode = await ProgramEntry.RunAsync(args, Console.Out, Console.Error);
return exitCode;
