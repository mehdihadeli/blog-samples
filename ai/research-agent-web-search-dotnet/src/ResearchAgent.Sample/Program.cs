namespace ResearchAgent.Sample;

internal static class Program
{
    public static Task<int> Main(string[] args) =>
        ProgramEntry.RunAsync(args, Console.Out, Console.Error, CancellationToken.None);
}
