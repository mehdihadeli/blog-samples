using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;

namespace Tests.Shared.TestBase;

public abstract class IntegrationTestBase<TEntryPoint, TDbContext, TSharedFixture>(
    TSharedFixture sharedFixture
) : IAsyncLifetime
    where TEntryPoint : class
    where TDbContext : DbContext
    where TSharedFixture : SharedFixture
{
    protected TSharedFixture SharedFixture { get; } = sharedFixture;
    protected HttpClient Client { get; private set; } = null!;
    protected IServiceProvider ServiceProvider { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await SharedFixture.ResetAsync();
        var factory = CreateFactory();
        Client = factory.CreateClient();
        ServiceProvider = factory.Services;
    }

    protected virtual CustomWebApplicationFactory<TEntryPoint> CreateFactory()
    {
        return new CustomWebApplicationFactory<TEntryPoint>()
            .WithSetting(
                "ConnectionStrings:ordersdb",
                SharedFixture.PostgresFixture.ConnectionString
            )
            .WithSetting(
                "ConnectionStrings:rabbitmq",
                SharedFixture.RabbitMqFixture.ConnectionString
            )
            .WithSetting("Messaging:Transport", "rabbitmq");
    }

    protected async Task ExecuteDbContextAsync(
        Func<TDbContext, Task> action,
        CancellationToken ct = default
    )
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await action(db);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
