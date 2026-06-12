namespace ResearchAgent.Sample;

internal sealed class CommandLineOptions
{
    private CommandLineOptions(
        string topic,
        int maxIterations,
        string? existingPostsPath,
        string? searchIndexPath
    )
    {
        Topic = topic;
        MaxIterations = maxIterations;
        ExistingPostsPath = existingPostsPath;
        SearchIndexPath = searchIndexPath;
    }

    public string Topic { get; }

    public int MaxIterations { get; }

    public string? ExistingPostsPath { get; }

    public string? SearchIndexPath { get; }

    public static CommandLineOptions Parse(string[] args)
    {
        string? topic = null;
        int maxIterations = 3;
        string? existingPostsPath = null;
        string? searchIndexPath = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--topic":
                    topic = ReadValue(args, ++index, "--topic requires a value.");
                    break;
                case "--max-iterations":
                    var value = ReadValue(args, ++index, "--max-iterations requires a value.");
                    if (!int.TryParse(value, out maxIterations) || maxIterations < 1)
                    {
                        throw new ArgumentException(
                            "--max-iterations must be an integer greater than 0."
                        );
                    }

                    break;
                case "--existing-posts":
                    existingPostsPath = ReadValue(
                        args,
                        ++index,
                        "--existing-posts requires a value."
                    );
                    break;
                case "--search-index":
                    searchIndexPath = ReadValue(args, ++index, "--search-index requires a value.");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException(
                "Usage: --topic <value> [--max-iterations <n>] [--existing-posts <path>] [--search-index <path>]"
            );
        }

        return new CommandLineOptions(
            topic.Trim(),
            maxIterations,
            existingPostsPath,
            searchIndexPath
        );
    }

    private static string ReadValue(string[] args, int index, string message)
    {
        if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new ArgumentException(message);
        }

        return args[index];
    }
}
