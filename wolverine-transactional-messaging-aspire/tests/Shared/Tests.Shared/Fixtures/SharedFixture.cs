using BuildingBlocks.Integration.Wolverine.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tests.Shared.Factory;
using Tests.Shared.TestBase;
using Wolverine;
using Wolverine.Tracking;
using Xunit;

namespace Tests.Shared.Fixtures;

public abstract class SharedFixture<TEntryPoint> : IAsyncLifetime
    where TEntryPoint : class
{
    private readonly bool _useMongo;
    private ITrackedSession? _lastTrackedSession;

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

    protected ITrackedSession LastTrackedSession =>
        _lastTrackedSession
        ?? throw new InvalidOperationException(
            "No Wolverine tracked session is available for the current test action."
        );

    public virtual async Task InitializeAsync()
    {
        await Postgres.InitializeAsync();
        await RabbitMq.InitializeAsync();
        await Kafka.InitializeAsync();

        if (Mongo is not null)
        {
            await Mongo.InitializeAsync();
        }
    }

    public virtual async Task DisposeAsync()
    {
        if (Mongo is not null)
        {
            await Mongo.DisposeAsync();
        }

        await Kafka.DisposeAsync();
        await RabbitMq.DisposeAsync();
        await Postgres.DisposeAsync();
    }

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

    public CustomWebApplicationFactory<TEntryPoint> CreateFactory(
        string transport,
        Action<CustomWebApplicationFactory<TEntryPoint>>? configure = null
    )
    {
        var factory = new CustomWebApplicationFactory<TEntryPoint>();
        ConfigureFactory(factory, transport);
        configure?.Invoke(factory);
        return factory;
    }

    public async ValueTask PublishMessageAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class
    {
        using var factory = CreateFactory(DefaultTransport);
        await PublishMessageAsync(factory, message, cancellationToken);
    }

    public async ValueTask PublishMessageAsync<TMessage>(
        CustomWebApplicationFactory<TEntryPoint> factory,
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var trackedSession = await PublishMessageInternalAsync(
            scope.ServiceProvider,
            message,
            cancellationToken
        );

        RememberTrackedSession(trackedSession);
    }

    private static async Task<ITrackedSession> PublishMessageInternalAsync<TMessage>(
        IServiceProvider serviceProvider,
        TMessage message,
        CancellationToken cancellationToken
    )
        where TMessage : class
    {
        var bus = serviceProvider.GetRequiredService<IExternalEventBus>();

        return await serviceProvider
            .TrackActivity()
            .ExecuteAndWaitAsync(
                (Func<IMessageContext, Task>)(
                    async _ => await bus.PublishAsync(message, cancellationToken)
                )
            );
    }

    // Ref: https://tech.energyhelpline.com/in-memory-testing-with-message-bus-abstractions/
    public async ValueTask WaitUntilConditionMet(
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
                    exception ?? $"Condition not met for the test in the '{timeoutExpired}' second."
                );
            }

            await Task.Delay(100);
            meet = await conditionToMet.Invoke();
            timeoutExpired = DateTime.Now - startTime > TimeSpan.FromSeconds(time);
        }
    }

    public Task ShouldPublish<T>()
        where T : class
    {
        LastTrackedSession.ShouldPublish<T>();
        return Task.CompletedTask;
    }

    public Task ShouldConsume<T>()
        where T : class
    {
        LastTrackedSession.ShouldConsume<T>();
        return Task.CompletedTask;
    }

    public Task ShouldConsume<TMessage, TConsumedBy>()
        where TMessage : class
        where TConsumedBy : class
    {
        LastTrackedSession.ShouldConsume<TMessage, TConsumedBy>();
        return Task.CompletedTask;
    }

    private void RememberTrackedSession(ITrackedSession trackedSession)
    {
        _lastTrackedSession = trackedSession;
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
