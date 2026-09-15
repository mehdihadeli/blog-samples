namespace ECommerce.Services.Catalogs.EndToEndTests;

[CollectionDefinition(Name)]
public sealed class EndToEndTestCollection : ICollectionFixture<CatalogsEndToEndSharedFixture>
{
    public const string Name = "catalogs-end-to-end-tests";
}
