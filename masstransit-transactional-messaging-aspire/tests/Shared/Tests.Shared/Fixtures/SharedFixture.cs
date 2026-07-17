using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tests.Shared.Factory;
using Xunit;

namespace Tests.Shared.Fixtures;

/// <summary>
/// Collection-scoped shared fixture that manages test container lifecycles and
/// creates WebApplicationFactory instances configured with MassTransit test harness
/// and transport-specific connection strings.
///
/// Inspired by the Wolverine SharedFixture pattern, adapted for MassTransit's ITestHarness.
/// </summary>
public abstract class SharedFixture<TEntryPoint> : IAsyncLifetime
    where TEntryPoint : class
{
    private readonly bool _useMongo;

    protected SharedFixture(bool useMongo = false)
    {
        _useMongo = useMongo;

        if (_useMongo)
        {
            Mongo = new MongoContainerFixture();
        }
    }

    public PostgresContainerFixture Postgres { get; } = new();

    public RabbitMqContainerFixture RabbitMq { get; } = new();

    public KafkaContainerFixture Kafka { get; } = new();

    public MongoContainerFixture? Mongo { get; }

    public virtual async ValueTask InitializeAsync()
    {
        await Postgres.InitializeAsync();
        await RabbitMq.InitializeAsync();
        await Kafka.InitializeAsync();

        if (Mongo is not null)
        {
            await Mongo.InitializeAsync();
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (Mongo is not null)
        {
            await Mongo.DisposeAsync();
        }

        await Kafka.DisposeAsync();
        await RabbitMq.DisposeAsync();
        await Postgres.DisposeAsync();
    }

    /// <summary>
    /// Reset database state and message broker state before each test.
    /// </summary>
    public async Task ResetAsync(string transport, CancellationToken cancellationToken = default)
    {
        await Postgres.ResetAsync();

        if (Mongo is not null)
        {
            await Mongo.ResetAsync(cancellationToken);
        }

        if (string.Equals(transport, "kafka", StringComparison.OrdinalIgnoreCase))
        {
            await Kafka.EnsureStartedAsync();
            await Kafka.CleanupTopicsAsync(cancellationToken);
            return;
        }

        await RabbitMq.EnsureStartedAsync();
        await RabbitMq.CleanupQueuesAsync(cancellationToken);
    }

    /// <summary>
    /// Create a WebApplicationFactory configured with MassTransit test harness,
    /// transport-specific connection strings, and any per-test customisation.
    /// </summary>
    public CustomWebApplicationFactory<TEntryPoint> CreateFactory(
        string transport,
        Action<CustomWebApplicationFactory<TEntryPoint>>? configure = null
    )
    {
        var factory = new CustomWebApplicationFactory<TEntryPoint>();

        // Test harness is registered conditionally inside the app's own
        // AddMassTransitMessaging when the hosting environment is "Test".
        // No need to add it here — see MassTransitServiceCollectionExtensions.

        ConfigureFactory(factory, transport);
        configure?.Invoke(factory);
        return factory;
    }

    // ─── Test-harness message assertions (MassTransit ITestHarness) ──────
    // Inspired by the Wolverine SharedFixture pattern:
    //   ShouldPublishing  →  harness.Published.Any<T>()
    //   ShouldSending     →  harness.Sent.Any<T>()
    //   ShouldConsuming   →  harness.Consumed.Any<T>()

    /// <summary>
    /// Assert that a message of type <typeparamref name="T"/> was published
    /// through the MassTransit bus. Uses the factory's <see cref="ITestHarness"/>.
    /// </summary>
    public async Task ShouldPublishing<T>(CustomWebApplicationFactory<TEntryPoint> factory)
        where T : class
    {
        var harness = factory.Services.GetRequiredService<ITestHarness>();
        await WaitUntilConditionMet(
            () => harness.Published.Any<T>(),
            timeoutSecond: 15,
            exception: $"Expected {typeof(T).Name} to have been published."
        );
    }

    /// <summary>
    /// Assert that a message of type <typeparamref name="T"/> was sent
    /// (via <c>ISendEndpoint</c>) during the test.
    /// </summary>
    public async Task ShouldSending<T>(CustomWebApplicationFactory<TEntryPoint> factory)
        where T : class
    {
        var harness = factory.Services.GetRequiredService<ITestHarness>();
        await WaitUntilConditionMet(
            () => harness.Sent.Any<T>(),
            timeoutSecond: 15,
            exception: $"Expected {typeof(T).Name} to have been sent."
        );
    }

    /// <summary>
    /// Assert that a message of type <typeparamref name="T"/> was consumed
    /// by any consumer during the test.
    /// </summary>
    public async Task ShouldConsuming<T>(CustomWebApplicationFactory<TEntryPoint> factory)
        where T : class
    {
        var harness = factory.Services.GetRequiredService<ITestHarness>();
        await WaitUntilConditionMet(
            () => harness.Consumed.Any<T>(),
            timeoutSecond: 15,
            exception: $"Expected {typeof(T).Name} to have been consumed."
        );
    }

    /// <summary>
    /// Assert that a message of type <typeparamref name="TMessage"/> was consumed
    /// by the specific consumer <typeparamref name="TConsumedBy"/>.
    /// </summary>
    public async Task ShouldConsuming<TMessage, TConsumedBy>(
        CustomWebApplicationFactory<TEntryPoint> factory
    )
        where TMessage : class
        where TConsumedBy : class, IConsumer
    {
        var harness = factory.Services.GetRequiredService<ITestHarness>();
        var consumerHarness = harness.GetConsumerHarness<TConsumedBy>();
        await WaitUntilConditionMet(
            () => consumerHarness.Consumed.Any<TMessage>(),
            timeoutSecond: 15,
            exception: $"Expected {typeof(TMessage).Name} consumed by {typeof(TConsumedBy).Name}."
        );
    }

    /// <summary>
    /// Poll until a condition is met (with timeout).
    /// Ref: https://tech.energyhelpline.com/in-memory-testing-with-message-bus-abstractions/
    /// </summary>
    public async Task WaitUntilConditionMet(
        Func<Task<bool>> conditionToMet,
        int? timeoutSecond = null,
        string? exception = null
    )
    {
        var time = timeoutSecond ?? 300;

        var startTime = DateTime.Now;
        var timeoutExpired = false;
        var meet = await conditionToMet.Invoke();

        while (!meet)
        {
            if (timeoutExpired)
            {
                throw new TimeoutException(
                    exception ?? $"Condition not met within '{time}' seconds."
                );
            }

            await Task.Delay(100);
            meet = await conditionToMet.Invoke();
            timeoutExpired = DateTime.Now - startTime > TimeSpan.FromSeconds(time);
        }
    }

    public async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var factory = CreateFactory(DefaultTransport);
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    public async Task<TResult> ExecuteScopeAsync<TResult>(
        Func<IServiceProvider, Task<TResult>> action
    )
    {
        using var factory = CreateFactory(DefaultTransport);
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    protected async Task ExecuteDbContextAsync<TContext>(
        CustomWebApplicationFactory<TEntryPoint> factory,
        Func<TContext, Task> action
    )
        where TContext : DbContext
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await EnsureSchemaCreatedAsync(dbContext);
        await action(dbContext);
    }

    protected async Task<TResult> ExecuteDbContextAsync<TContext, TResult>(
        CustomWebApplicationFactory<TEntryPoint> factory,
        Func<TContext, Task<TResult>> action
    )
        where TContext : DbContext
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await EnsureSchemaCreatedAsync(dbContext);
        return await action(dbContext);
    }

    protected virtual string DefaultTransport => "rabbitmq";

    /// <summary>
    /// Subclasses set connection-string overrides and any other transport-specific config.
    /// </summary>
    protected abstract void ConfigureFactory(
        CustomWebApplicationFactory<TEntryPoint> factory,
        string transport
    );

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
