using System.Net.Http.Headers;
using BuildingBlocks.Abstractions.Messages;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Tests.Shared.Factory;
using Wolverine;
using Wolverine.Tracking;
using Xunit.Sdk;

namespace Tests.Shared.Fixtures;

public abstract class SharedFixture<TEntryPoint>(
    bool usePostgres = false,
    bool useRabbitMq = false,
    bool useKafka = false,
    bool useMongo = false
) : IAsyncLifetime
    where TEntryPoint : class
{
    private readonly IMessageSink? _messageSink;
    private ITrackedSession? _lastTrackedSession;
    private CustomWebApplicationFactory<TEntryPoint>? _factory;
    public IServiceProvider ServiceProvider => field ??= Factory.Services;

    public IConfiguration Configuration =>
        field ??= ServiceProvider.GetRequiredService<IConfiguration>();
    public IHttpContextAccessor HttpContextAccessor =>
        field ??= ServiceProvider.GetRequiredService<IHttpContextAccessor>();

    public PostgresContainerFixture? Postgres { get; } =
        usePostgres ? new PostgresContainerFixture() : null;

    public RabbitMqContainerFixture? RabbitMq { get; } =
        useRabbitMq ? new RabbitMqContainerFixture() : null;

    public KafkaContainerFixture? Kafka { get; } = useKafka ? new KafkaContainerFixture() : null;

    public MongoContainerFixture? Mongo { get; } = useMongo ? new MongoContainerFixture() : null;

    public HttpClient GuestClient
    {
        get
        {
            if (field == null)
            {
                field = Factory.CreateClient();
                // Set the media type of the request to JSON - we need this for getting problem details result for all http calls because problem details just return response for request with media type JSON
                field.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                );
            }

            return field;
        }
    }

    private CustomWebApplicationFactory<TEntryPoint> Factory => _factory ??= CreateTestFactory();

    protected SharedFixture(IMessageSink messageSink)
        : this()
    {
        _messageSink = messageSink;
        _factory = CreateTestFactory();
    }

    public virtual async ValueTask InitializeAsync()
    {
        if (Postgres is not null)
            await Postgres.InitializeAsync();
        if (RabbitMq is not null)
            await RabbitMq.InitializeAsync();
        if (Kafka is not null)
            await Kafka.InitializeAsync();
        if (Mongo is not null)
            await Mongo.InitializeAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (Mongo is not null)
            await Mongo.DisposeAsync();
        if (Kafka is not null)
            await Kafka.DisposeAsync();
        if (RabbitMq is not null)
            await RabbitMq.DisposeAsync();
        if (Postgres is not null)
            await Postgres.DisposeAsync();
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (Postgres is not null)
            await Postgres.ResetAsync();
        if (Mongo is not null)
            await Mongo.ResetAsync(cancellationToken);
        if (Kafka is not null)
        {
            await Kafka.EnsureStartedAsync();
            await Kafka.CleanupTopicsAsync(cancellationToken);
        }
        if (RabbitMq is not null)
        {
            await RabbitMq.EnsureStartedAsync();
            await RabbitMq.CleanupQueuesAsync(cancellationToken);
        }
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        if (Postgres is not null)
            await Postgres.ResetAsync();
        if (Mongo is not null)
            await Mongo.ResetAsync(cancellationToken);
        if (RabbitMq is not null)
            await RabbitMq.CleanupQueuesAsync(cancellationToken);
    }

    private CustomWebApplicationFactory<TEntryPoint> CreateTestFactory()
    {
        var factory = new CustomWebApplicationFactory<TEntryPoint>();

        factory.WithTestConfigureServices(ApplyTestConfigureServices);
        factory.WithTestConfigureAppConfiguration(ApplyTestConfigureAppConfiguration);
        factory.WithTestConfiguration(ApplyTestConfiguration);
        factory.AddOverrideEnvKeyValues(ApplyOverrideEnvKeyValues);
        factory.AddOverrideInMemoryConfig(ApplyOverrideInMemoryConfig);

        return factory;
    }

    protected virtual void ApplyOverrideInMemoryConfig(IDictionary<string, string> dictionary) { }

    protected virtual void ApplyOverrideEnvKeyValues(IDictionary<string, string> dictionary) { }

    protected virtual void ApplyTestConfiguration(IConfiguration configuration) { }

    protected virtual void ApplyTestConfigureAppConfiguration(
        WebHostBuilderContext context,
        IConfigurationBuilder builder
    ) { }

    protected virtual void ApplyTestConfigureServices(IServiceCollection collection) { }

    /// <summary>
    /// Wraps an action in a tracked session and asserts <typeparamref name="T"/> was published.
    /// </summary>
    public async Task ShouldPublishing<T>(
        Func<Task> action,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var trackedSession = await Factory
            .Services.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(30))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ => await action()));

        var sentEnvelopes = trackedSession
            .FindEnvelopesWithMessageType<T>(MessageEventType.Sent)
            .ToArray();

        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();

        if (sentEnvelopes.Length != 0)
        {
            return;
        }
    }

    /// <summary>
    /// Wraps an action in a tracked session and asserts <typeparamref name="T"/> was sent.
    /// </summary>
    public async Task ShouldSending<T>(
        Func<Task> action,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var trackedSession = await Factory
            .Services.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(30))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ => await action()));

        trackedSession.FindEnvelopesWithMessageType<T>(MessageEventType.Sent).ShouldNotBeEmpty();
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();
    }

    /// <summary>
    /// Wraps an action in a tracked session and asserts <typeparamref name="T"/> was consumed.
    /// </summary>
    public async Task ShouldConsuming<T>(
        Func<Task> action,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var trackedSession = await Factory
            .Services.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(30))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ => await action()));

        trackedSession
            .FindEnvelopesWithMessageType<T>(MessageEventType.MessageSucceeded)
            .ShouldNotBeEmpty();
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();
    }

    public async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default
    )
    {
        return await ExecuteScopeAsync(async sp =>
        {
            var mediator = sp.GetRequiredService<IMediator>();

            return await mediator.Send(request, cancellationToken);
        });
    }

    public async ValueTask PublishMessageAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class, BuildingBlocks.Abstractions.Messages.IMessage
    {
        var trackedSession = await ExecuteScopeAsync(async sp =>
        {
            var bus = sp.GetRequiredService<IExternalEventBus>();

            return await sp.TrackActivity()
                .ExecuteAndWaitAsync(
                    (Func<IMessageContext, Task>)(
                        async _ => await bus.PublishAsync(message, cancellationToken)
                    )
                );
        });

        RememberTrackedSession(trackedSession);
    }

    public async ValueTask PublishMessageAsync<TMessage>(
        MessageEnvelope<TMessage> messageEnvelope,
        CancellationToken cancellationToken = default
    )
        where TMessage : class, BuildingBlocks.Abstractions.Messages.IMessage
    {
        var trackedSession = await ExecuteScopeAsync(async sp =>
        {
            var bus = sp.GetRequiredService<IExternalEventBus>();

            return await sp.TrackActivity()
                .ExecuteAndWaitAsync(
                    (Func<IMessageContext, Task>)(
                        async _ => await bus.PublishAsync(messageEnvelope, cancellationToken)
                    )
                );
        });

        RememberTrackedSession(trackedSession);
    }

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
                    exception ?? $"Condition not met for the test in the '{timeoutSecond}' second."
                );
            }

            await Task.Delay(100);
            meet = await conditionToMet.Invoke();
            timeoutExpired = DateTime.Now - startTime > TimeSpan.FromSeconds(time);
        }
    }

    public async Task ShouldPublishing<T>(CancellationToken cancellationToken = default)
        where T : class
    {
        var trackedSession = GetTrackedSession();
        var sentEnvelopes = trackedSession
            .FindEnvelopesWithMessageType<T>(MessageEventType.Sent)
            .ToArray();

        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();

        if (sentEnvelopes.Length != 0)
        {
            return;
        }
    }

    public async Task ShouldSending<T>(CancellationToken cancellationToken = default)
        where T : class
    {
        var trackedSession = GetTrackedSession();

        trackedSession.FindEnvelopesWithMessageType<T>(MessageEventType.Sent).ShouldNotBeEmpty();
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();

        await Task.CompletedTask;
    }

    public async Task ShouldSendingInternalCommand<T>(CancellationToken cancellationToken = default)
        where T : class, IInternalCommand
    {
        var trackedSession = GetTrackedSession();

        trackedSession.FindEnvelopesWithMessageType<T>(MessageEventType.Sent).ShouldNotBeEmpty();
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();

        await Task.CompletedTask;
    }

    public async Task ShouldConsuming<T>(CancellationToken cancellationToken = default)
        where T : class
    {
        var trackedSession = GetTrackedSession();

        trackedSession
            .FindEnvelopesWithMessageType<T>(MessageEventType.MessageSucceeded)
            .ShouldNotBeEmpty();
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();

        await Task.CompletedTask;
    }

    public async Task ShouldConsuming<TMessage, TConsumedBy>(
        CancellationToken cancellationToken = default
    )
        where TMessage : class
        where TConsumedBy : class
    {
        await ShouldConsuming<TMessage>(cancellationToken);
    }

    private ITrackedSession GetTrackedSession()
    {
        return _lastTrackedSession
            ?? throw new InvalidOperationException(
                "No Wolverine tracked session is available for the current test action."
            );
    }

    private void RememberTrackedSession(ITrackedSession trackedSession)
    {
        _lastTrackedSession = trackedSession;
    }
}
