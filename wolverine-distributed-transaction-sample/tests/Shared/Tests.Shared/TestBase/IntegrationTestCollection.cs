using Xunit;

namespace Tests.Shared.TestBase;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection
{
    public const string Name = "integration-tests";
}
