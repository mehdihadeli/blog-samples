using ECommerce.Services.Catalogs.Shared.Data;
using Tests.Shared.TestBase;

namespace ECommerce.Services.Catalogs.EndToEndTests;

[Collection(EndToEndTestCollection.Name)]
public abstract class CatalogsEndToEndTestBase
    : IntegrationTestBase<Program, CatalogsEndToEndSharedFixture>
{
    protected CatalogsEndToEndTestBase(CatalogsEndToEndSharedFixture sharedFixture)
        : base(sharedFixture) { }

    protected override async Task ResetStateAsync()
    {
        await ExecuteCatalogsDbContextAsync(_ => Task.CompletedTask);
    }

    protected Task ExecuteCatalogsDbContextAsync(Func<CatalogsDbContext, Task> action)
    {
        return ExecuteDbContextAsync(action);
    }
}
