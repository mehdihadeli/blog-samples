using Xunit;

namespace ECommerce.Services.Catalogs.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<CatalogsSharedFixture>
{
    public const string Name = "catalogs-integration-tests";
}
