using Xunit;

namespace Tests.Shared.TestBase;

/// <summary>
/// Marker collection that xUnit uses to guarantee sequential execution and
/// shared fixture scope. Concrete <see cref="SharedFixture{TEntryPoint}"/>
/// instances are registered as collection fixtures by each test project
/// (e.g. Catalogs.IntegrationTests.IntegrationTestCollection).
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection
{
    public const string Name = "integration-tests";
}
