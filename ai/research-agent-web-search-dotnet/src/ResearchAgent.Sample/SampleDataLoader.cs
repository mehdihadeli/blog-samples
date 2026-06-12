using System.Text.Json;

namespace ResearchAgent.Sample;

internal sealed class SampleDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly string _baseDirectory;

    public SampleDataLoader(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    public async Task<IReadOnlyList<ExistingPost>> LoadExistingPostsAsync(
        string? path,
        CancellationToken cancellationToken
    )
    {
        var resolvedPath = ResolvePath(path, "existing-posts.json");
        await using var stream = File.OpenRead(resolvedPath);
        var posts = await JsonSerializer.DeserializeAsync<List<ExistingPost>>(
            stream,
            JsonOptions,
            cancellationToken: cancellationToken
        );
        return posts ?? [];
    }

    public async Task<IReadOnlyList<SearchDocument>> LoadSearchDocumentsAsync(
        string? path,
        CancellationToken cancellationToken
    )
    {
        var resolvedPath = ResolvePath(path, "search-index.json");
        await using var stream = File.OpenRead(resolvedPath);
        var documents = await JsonSerializer.DeserializeAsync<List<SearchDocument>>(
            stream,
            JsonOptions,
            cancellationToken: cancellationToken
        );
        return documents ?? [];
    }

    private string ResolvePath(string? overridePath, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        var sampleRoot = Path.GetFullPath(
            Path.Combine(_baseDirectory, "..", "..", "..", "..", "..")
        );
        return Path.Combine(sampleRoot, "sample-data", fileName);
    }
}
