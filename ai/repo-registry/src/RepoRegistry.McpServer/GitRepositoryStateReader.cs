using System.Diagnostics;

namespace RepoRegistry.McpServer;

internal interface IRepositoryStateReader
{
    RepositoryState Read(RepoDefinition repo);
}

internal sealed class GitRepositoryStateReader : IRepositoryStateReader
{
    public RepositoryState Read(RepoDefinition repo)
    {
        if (!Directory.Exists(repo.Path))
        {
            return new RepositoryState(false, repo.Path, "missing", "missing", false, 0);
        }

        var branch = Git(repo.Path, "rev-parse", "--abbrev-ref", "HEAD") ?? "unknown";
        var commit = Git(repo.Path, "rev-parse", "--short", "HEAD") ?? "unknown";
        var status = Git(repo.Path, "status", "--short") ?? string.Empty;
        var uncommittedCount = status
            .Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Length;

        return new RepositoryState(
            true,
            repo.Path,
            branch,
            commit,
            uncommittedCount > 0,
            uncommittedCount
        );
    }

    private static string? Git(string repoPath, params string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(repoPath);
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            process.WaitForExit(10_000);
            return process.ExitCode == 0 ? process.StandardOutput.ReadToEnd().Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
