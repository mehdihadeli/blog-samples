using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;
using Xunit;

namespace Tests.Shared.TestBase;

/// <summary>
/// Base class for integration tests that follow the shared-fixture pattern.
///
/// A collection-scoped <typeparamref name="TSharedFixture"/> manages container lifecycles.
/// Per test, a fresh <see cref="WebApplicationFactory{TEntryPoint}"/> is created with
/// <c>AddMassTransitTestHarness</c>, giving each test isolated state and a clean
/// <see cref="ITestHarness"/> for asserting on published and consumed messages.
///
/// Inspired by the Wolverine integration test pattern, adapted for MassTransit.
/// </summary>
public abstract class IntegrationTestBase<TEntryPoint, TSharedFixture> : IAsyncLifetime
    where TEntryPoint : class
    where TSharedFixture : SharedFixture<TEntryPoint>
{
    private CustomWebApplicationFactory<TEntryPoint>? _factory;

    protected IntegrationTestBase(TSharedFixture sharedFixture)
    {
        SharedFixture = sharedFixture;
    }

    protected TSharedFixture SharedFixture { get; }

    protected PostgresContainerFixture Postgres => SharedFixture.Postgres;

    protected RabbitMqContainerFixture RabbitMq => SharedFixture.RabbitMq;

    protected KafkaContainerFixture Kafka => SharedFixture.Kafka;

    protected virtual string MessagingTransport => "rabbitmq";

    /// <summary>
    /// The per-test WebApplicationFactory. Created fresh in <see cref="InitializeAsync"/>.
    /// </summary>
    protected CustomWebApplicationFactory<TEntryPoint> Factory =>
        _factory
        ?? throw new InvalidOperationException(
            "Factory not initialised. Ensure InitializeAsync is called before accessing Factory."
        );

    public virtual async ValueTask InitializeAsync()
    {
        await SharedFixture.ResetAsync(MessagingTransport);
        _factory = SharedFixture.CreateFactory(MessagingTransport, ConfigureFactory);
        await ResetStateAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }
    }

    protected virtual void ConfigureFactory(CustomWebApplicationFactory<TEntryPoint> factory) { }

    protected virtual Task ResetStateAsync() => Task.CompletedTask;

    // ─── MassTransit test-harness message assertions ──────────────────────
    // Named to match the Wolverine pattern: ShouldPublishing, ShouldSending,
    // ShouldConsuming.  Adapted for MassTransit's ITestHarness.

    /// <summary>
    /// Assert that a message of type <typeparamref name="T"/> was published
    /// through the MassTransit bus during the test.
    /// </summary>
    protected async Task ShouldPublishing<T>()
        where T : class
    {
        await SharedFixture.ShouldPublishing<T>(Factory);
    }

    /// <summary>
    /// Assert that a message of type <typeparamref name="T"/> was sent
    /// (via <c>ISendEndpoint</c>) during the test.
    /// </summary>
    protected async Task ShouldSending<T>()
        where T : class
    {
        await SharedFixture.ShouldSending<T>(Factory);
    }

    /// <summary>
    /// Assert that a message of type <typeparamref name="T"/> was consumed
    /// by any consumer during the test.
    /// </summary>
    protected async Task ShouldConsuming<T>()
        where T : class
    {
        await SharedFixture.ShouldConsuming<T>(Factory);
    }

    /// <summary>
    /// Assert that a message of type <typeparamref name="TMessage"/> was consumed
    /// by the specific consumer <typeparamref name="TConsumedBy"/>.
    /// </summary>
    protected async Task ShouldConsuming<TMessage, TConsumedBy>()
        where TMessage : class
        where TConsumedBy : class, IConsumer
    {
        await SharedFixture.ShouldConsuming<TMessage, TConsumedBy>(Factory);
    }

    // ─── DbContext helpers ────────────────────────────────────────────────

    protected async Task ExecuteDbContextAsync<TContext>(Func<TContext, Task> action)
        where TContext : DbContext
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await EnsureSchemaCreatedAsync(dbContext);
        await action(dbContext);
    }

    protected async Task<TResult> ExecuteDbContextAsync<TContext, TResult>(
        Func<TContext, Task<TResult>> action
    )
        where TContext : DbContext
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await EnsureSchemaCreatedAsync(dbContext);
        return await action(dbContext);
    }

    private static async Task EnsureSchemaCreatedAsync(DbContext dbContext)
    {
        var databaseCreator = dbContext.Database.GetService<IRelationalDatabaseCreator>();

        if (!await databaseCreator.ExistsAsync())
        {
            await dbContext.Database.EnsureCreatedAsync();
            return;
        }

        await dbContext.Database.EnsureCreatedAsync();

        try
        {
            await databaseCreator.CreateTablesAsync();
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.DuplicateTable)
        {
            // EF tables for this context already exist.
        }
    }
}
