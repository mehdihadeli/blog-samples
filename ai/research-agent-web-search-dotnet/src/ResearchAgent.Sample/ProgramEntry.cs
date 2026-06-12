using System.Text.Json;

namespace ResearchAgent.Sample;

internal static class ProgramEntry
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var options = CommandLineOptions.Parse(args);
            var loader = new SampleDataLoader(AppContext.BaseDirectory);
            var existingPosts = await loader.LoadExistingPostsAsync(
                options.ExistingPostsPath,
                cancellationToken
            );
            var searchDocuments = await loader.LoadSearchDocumentsAsync(
                options.SearchIndexPath,
                cancellationToken
            );

            var agent = new ResearchAgent(existingPosts, new FakeWebSearchClient(searchDocuments));
            var brief = await agent.CreateBriefAsync(
                options.Topic,
                options.MaxIterations,
                cancellationToken
            );

            var json = JsonSerializer.Serialize(
                brief,
                new JsonSerializerOptions { WriteIndented = true }
            );

            await standardOutput.WriteLineAsync(json);
            return 0;
        }
        catch (ArgumentException ex)
        {
            await standardError.WriteLineAsync(ex.Message);
            return 1;
        }
    }
}
