using ECommerce.Services.Catalogs.Shared.Data;
using Tests.Shared.TestBase;

namespace ECommerce.Services.Catalogs.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public abstract class CatalogsIntegrationTestBase
    : IntegrationTestBase<Program, CatalogsSharedFixture>
{
    protected CatalogsIntegrationTestBase(CatalogsSharedFixture sharedFixture)
        : base(sharedFixture) { }

    protected override async Task ResetStateAsync()
    {
        await ExecuteCatalogsDbContextAsync(_ => Task.CompletedTask);
    }

    protected Task ExecuteCatalogsDbContextAsync(Func<CatalogsDbContext, Task> action)
    {
        return ExecuteDbContextAsync(action);
    }

    protected Task<TResult> ExecuteCatalogsDbContextAsync<TResult>(
        Func<CatalogsDbContext, Task<TResult>> action
    )
    {
        return ExecuteDbContextAsync<CatalogsDbContext, TResult>(action);
    }
}
