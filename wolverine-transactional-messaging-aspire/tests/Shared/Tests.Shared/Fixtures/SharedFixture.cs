using System.Net.Http.Headers;
using BuildingBlocks.Core.Messages;
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

// https://wolverinefx.net/guide/testing.html
// https://jeremydmiller.com/2022/12/12/introducing-wolverine-for-effective-server-side-net-development/
// https://jeremydmiller.com/2022/12/13/how-wolverine-allows-for-easier-testing/
public abstract class SharedFixture<TEntryPoint>(
    bool usePostgres = false,
    bool useRabbitMq = false,
    bool useKafka = false,
    bool useMongo = false
) : IAsyncLifetime
    where TEntryPoint : class
{
    private readonly IMessageSink? _messageSink;
    private CustomWebApplicationFactory<TEntryPoint>? _factory;

    private IServiceProvider? _serviceProvider;
    private IConfiguration? _configuration;
    private IHttpContextAccessor? _httpContextAccessor;
    private HttpClient? _guestClient;

    public IServiceProvider ServiceProvider => _serviceProvider ??= Factory.Services;
    public IConfiguration Configuration =>
        _configuration ??= ServiceProvider.GetRequiredService<IConfiguration>();
    public IHttpContextAccessor HttpContextAccessor =>
        _httpContextAccessor ??= ServiceProvider.GetRequiredService<IHttpContextAccessor>();

    public PostgresContainerFixture? Postgres { get; } =
        usePostgres ? new PostgresContainerFixture() : null;

    public RabbitMqContainerFixture? RabbitMq { get; } =
        useRabbitMq ? new RabbitMqContainerFixture() : null;

    public KafkaContainerFixture? Kafka { get; } = useKafka ? new KafkaContainerFixture() : null;

    public MongoContainerFixture? Mongo { get; } = useMongo ? new MongoContainerFixture() : null;

    /// <summary>
    /// Per-test timeout shared by every TrackActivity helper and polling loop in this
    /// fixture. Container startup is a collection-fixture concern (runs once per test
    /// class, not per test), so 90s is generous headroom for real broker round-trips
    /// (RabbitMQ/Kafka), outbox flush, and read-model projection waits.
    /// </summary>
    public TimeSpan TestTimeout => TimeSpan.FromSeconds(90);

    public HttpClient GuestClient
    {
        get
        {
            if (_guestClient == null)
            {
                _guestClient = Factory.CreateClient();
                // Set the media type of the request to JSON - we need this for getting problem details result for all http calls because problem details just return response for request with media type JSON
                _guestClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                );
            }

            return _guestClient;
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
        _factory?.Dispose();

        if (Mongo is not null)
            await Mongo.DisposeAsync();
        if (Kafka is not null)
            await Kafka.DisposeAsync();
        if (RabbitMq is not null)
            await RabbitMq.DisposeAsync();
        if (Postgres is not null)
            await Postgres.DisposeAsync();
    }

    /// <summary>
    /// Whether broker state (Kafka topics, RabbitMQ queue contents) is wiped between
    /// tests. Default true so each test starts from a clean broker. Set to false when
    /// the broker topology must survive across tests in a collection — e.g. Kafka
    /// building-block tests, where the shared host is long-lived and Wolverine's
    /// AutoProvision only creates topics at startup, so deleting topics mid-run would
    /// leave listeners subscribed to removed partitions.
    /// </summary>
    protected virtual bool ResetBrokerStateBetweenTests => true;

    public virtual async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (Postgres is not null)
            await Postgres.ResetAsync();
        if (Mongo is not null)
            await Mongo.ResetAsync(cancellationToken);

        if (ResetBrokerStateBetweenTests)
        {
            if (Kafka is not null)
                await Kafka.ResetAsync(cancellationToken);
            if (RabbitMq is not null)
                await RabbitMq.ResetAsync(cancellationToken);
        }

        // Force the test host to start (it builds lazily on first ServiceProvider
        // access) so Wolverine provisions the broker topology — exchanges, queues,
        // bindings — before any test queries the management API or publishes.
        // Without this, the first test races ahead of topology provisioning and
        // sees only the broker's default amq.* exchanges.
        _ = ServiceProvider;
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
    /// Wraps action in TrackActivity, asserts <typeparamref name="T"/> was received by handler
    /// AND completed successfully. Set <paramref name="includeExternalTransports"/> for broker
    /// round-trips (RabbitMQ/Kafka).
    /// </summary>
    public async Task ShouldConsuming<T>(
        Func<IMessageContext, Task> action,
        bool includeExternalTransports = false,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        // Hard-cap at TestTimeout so a stalled broker round-trip can never hang the
        // run past 90s: the outer WaitAsync backs up Wolverine's own session timeout
        // and honors the test-level cancellation token.
        var trackedSession = await BuildSession(includeExternalTransports)
            .ExecuteAndWaitAsync(action)
            .WaitAsync(TestTimeout, cancellationToken);

        // Message arrived at handler
        trackedSession
            .FindEnvelopesWithMessageType<T>(MessageEventType.Received)
            .ShouldNotBeEmpty();

        // Handler executed successfully
        trackedSession
            .FindEnvelopesWithMessageType<T>(MessageEventType.MessageSucceeded)
            .ShouldNotBeEmpty();

        // No faults published
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();
    }

    /// <summary>
    /// Wraps action in TrackActivity (no IMessageContext), asserts <typeparamref name="T"/> consumed.
    /// </summary>
    public async Task ShouldConsuming<T>(
        Func<Task> action,
        CancellationToken cancellationToken = default,
        bool includeExternalTransports = false
    )
        where T : class
    {
        await ShouldConsuming<T>(
            (Func<IMessageContext, Task>)(async _ => await action()),
            includeExternalTransports,
            cancellationToken
        );
    }

    /// <summary>
    /// Wraps action in TrackActivity, asserts <typeparamref name="T"/> was received AND handled
    /// successfully (consumer side), then runs <paramref name="assertSideEffect"/> to verify the
    /// consumer's own side-effect after consuming (e.g. the DbContext-mapped inbox row persisted
    /// via the EF Core envelope transaction, or the write model written by the handler).
    /// Set <paramref name="includeExternalTransports"/> for broker round-trips (RabbitMQ/Kafka).
    /// </summary>
    public async Task ShouldConsuming<T>(
        Func<IMessageContext, Task> action,
        Func<Task> assertSideEffect,
        bool includeExternalTransports = false,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        // Hard-cap at TestTimeout so a stalled broker round-trip can never hang the
        // run past 90s: the outer WaitAsync backs up Wolverine's own session timeout
        // and honors the test-level cancellation token.
        var trackedSession = await BuildSession(includeExternalTransports)
            .ExecuteAndWaitAsync(action)
            .WaitAsync(TestTimeout, cancellationToken);

        // Message arrived at handler
        trackedSession
            .FindEnvelopesWithMessageType<T>(MessageEventType.Received)
            .ShouldNotBeEmpty();

        // Handler executed successfully
        trackedSession
            .FindEnvelopesWithMessageType<T>(MessageEventType.MessageSucceeded)
            .ShouldNotBeEmpty();

        // No faults published
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();

        // Verify the consumer's side-effect (e.g. inbox handled-copy row, write model)
        await assertSideEffect();
    }

    /// <summary>
    /// Wraps action in TrackActivity (no IMessageContext), asserts <typeparamref name="T"/>
    /// consumed, then runs <paramref name="assertSideEffect"/> (see overload above).
    /// </summary>
    public async Task ShouldConsuming<T>(
        Func<Task> action,
        Func<Task> assertSideEffect,
        bool includeExternalTransports = false,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        await ShouldConsuming<T>(
            (Func<IMessageContext, Task>)(async _ => await action()),
            assertSideEffect,
            includeExternalTransports,
            cancellationToken
        );
    }

    /// <summary>
    /// Wraps action in TrackActivity, asserts <typeparamref name="T"/> was sent
    /// (IMessageBus.SendAsync). Set <paramref name="includeExternalTransports"/>
    /// for broker round-trips.
    /// </summary>
    public async Task ShouldSending<T>(
        Func<IMessageContext, Task> action,
        bool includeExternalTransports = false,
        Func<Type, bool>? ignoreMessageTypes = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        // Hard-cap at TestTimeout so a stalled broker round-trip can never hang the
        // run past 90s: the outer WaitAsync backs up Wolverine's own session timeout
        // and honors the test-level cancellation token.
        var trackedSession = await BuildSession(includeExternalTransports, ignoreMessageTypes)
            .ExecuteAndWaitAsync(action)
            .WaitAsync(TestTimeout, cancellationToken);

        trackedSession.FindEnvelopesWithMessageType<T>(MessageEventType.Sent).ShouldNotBeEmpty();
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();
    }

    /// <summary>
    /// Wraps action in TrackActivity (no IMessageContext), asserts <typeparamref name="T"/> sent.
    /// </summary>
    public async Task ShouldSending<T>(
        Func<Task> action,
        CancellationToken cancellationToken = default,
        bool includeExternalTransports = false,
        Func<Type, bool>? ignoreMessageTypes = null
    )
        where T : class
    {
        await ShouldSending<T>(
            (Func<IMessageContext, Task>)(async _ => await action()),
            includeExternalTransports,
            ignoreMessageTypes,
            cancellationToken
        );
    }

    /// <summary>
    /// Wraps action in TrackActivity, asserts <typeparamref name="T"/> was published
    /// (IMessageBus.PublishAsync). Set <paramref name="includeExternalTransports"/>
    /// for broker round-trips.
    /// </summary>
    public async Task ShouldPublishing<T>(
        Func<IMessageContext, Task> action,
        bool includeExternalTransports = false,
        Func<Type, bool>? ignoreMessageTypes = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        // Hard-cap at TestTimeout so a stalled broker round-trip can never hang the
        // run past 90s: the outer WaitAsync backs up Wolverine's own session timeout
        // and honors the test-level cancellation token.
        var trackedSession = await BuildSession(includeExternalTransports, ignoreMessageTypes)
            .ExecuteAndWaitAsync(action)
            .WaitAsync(TestTimeout, cancellationToken);

        trackedSession.FindEnvelopesWithMessageType<T>(MessageEventType.Sent).ShouldNotBeEmpty();
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();
    }

    /// <summary>
    /// Wraps action in TrackActivity (no IMessageContext), asserts <typeparamref name="T"/> published.
    /// </summary>
    public async Task ShouldPublishing<T>(
        Func<Task> action,
        CancellationToken cancellationToken = default,
        bool includeExternalTransports = false,
        Func<Type, bool>? ignoreMessageTypes = null
    )
        where T : class
    {
        await ShouldPublishing<T>(
            (Func<IMessageContext, Task>)(async _ => await action()),
            includeExternalTransports,
            ignoreMessageTypes,
            cancellationToken
        );
    }

    /// <summary>
    /// Wraps action in TrackActivity, asserts the outbox message <typeparamref name="T"/> was
    /// flushed and SENT after the publishing transaction committed, with no faults. This is the
    /// publisher-side proof that the message went through the transactional outbox (enrolled
    /// DbContext → persisted with the business transaction → flushed on commit).
    /// External-transport tracking is intentionally OFF: with it on, an outgoing record only
    /// completes once the receiver's <c>Received</c> event arrives, and in these tests the
    /// consuming service is a separate process, so the session would time out waiting on a
    /// receive that can never happen. The <c>Sent</c> record is still tracked and proves the
    /// flush path handed the message to the broker. Optional <paramref name="assertOutbox"/>
    /// can verify outbox artifacts afterwards (e.g. outgoing envelope rows via the
    /// DbContext-mapped table).
    /// </summary>
    public async Task ShouldProcessingOutboxMessage<T>(
        Func<IMessageContext, Task> action,
        Func<Task>? assertOutbox = null,
        Func<Type, bool>? ignoreMessageTypes = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        // Hard-cap at TestTimeout so a stalled outbox flush can never hang the run
        // past 90s: the outer WaitAsync backs up Wolverine's own session timeout
        // and honors the test-level cancellation token.
        var trackedSession = await BuildSession(
                includeExternalTransports: false,
                ignoreMessageTypes: ignoreMessageTypes
            )
            .ExecuteAndWaitAsync(action)
            .WaitAsync(TestTimeout, cancellationToken);

        // Outbox message was flushed and handed to the broker after commit
        trackedSession.FindEnvelopesWithMessageType<T>(MessageEventType.Sent).ShouldNotBeEmpty();

        // No faults published
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();

        if (assertOutbox is not null)
        {
            await assertOutbox();
        }
    }

    /// <summary>
    /// Wraps action in TrackActivity (no IMessageContext), asserts outbox message
    /// <typeparamref name="T"/> was sent (see overload above).
    /// </summary>
    public async Task ShouldProcessingOutboxMessage<T>(
        Func<Task> action,
        Func<Task>? assertOutbox = null,
        Func<Type, bool>? ignoreMessageTypes = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        await ShouldProcessingOutboxMessage<T>(
            (Func<IMessageContext, Task>)(async _ => await action()),
            assertOutbox,
            ignoreMessageTypes,
            cancellationToken
        );
    }

    /// <summary>
    /// Wraps action in TrackActivity, asserts the internal command <typeparamref name="T"/>
    /// (e.g. a read-model projection) was executed successfully after the main handler ran,
    /// with no faults. Optional <paramref name="assertSideEffect"/> can verify the command's
    /// side-effect (e.g. the read model actually upserted in Mongo).
    /// </summary>
    public async Task ShouldProcessingInternalCommand<T>(
        Func<IMessageContext, Task> action,
        Func<Task>? assertSideEffect = null,
        bool includeExternalTransports = false,
        Func<Type, bool>? ignoreMessageTypes = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        // Hard-cap at TestTimeout so a stalled internal command can never hang the
        // run past 90s: the outer WaitAsync backs up Wolverine's own session timeout
        // and honors the test-level cancellation token.
        var trackedSession = await BuildSession(
                includeExternalTransports,
                ignoreMessageTypes: ignoreMessageTypes
            )
            .ExecuteAndWaitAsync(action)
            .WaitAsync(TestTimeout, cancellationToken);

        // Internal command executed successfully
        trackedSession
            .FindEnvelopesWithMessageType<T>(MessageEventType.MessageSucceeded)
            .ShouldNotBeEmpty();

        // No faults published
        trackedSession
            .FindEnvelopesWithMessageType<Fault<T>>(MessageEventType.AutoFaultPublished)
            .ShouldBeEmpty();

        if (assertSideEffect is not null)
        {
            await assertSideEffect();
        }
    }

    /// <summary>
    /// Wraps action in TrackActivity (no IMessageContext), asserts internal command
    /// <typeparamref name="T"/> executed successfully (see overload above).
    /// </summary>
    public async Task ShouldProcessingInternalCommand<T>(
        Func<Task> action,
        Func<Task>? assertSideEffect = null,
        bool includeExternalTransports = false,
        Func<Type, bool>? ignoreMessageTypes = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        await ShouldProcessingInternalCommand<T>(
            (Func<IMessageContext, Task>)(async _ => await action()),
            assertSideEffect,
            includeExternalTransports,
            ignoreMessageTypes,
            cancellationToken
        );
    }

    /// <summary>
    /// Build TrackActivity session with optional external transport tracking and message-type
    /// filtering. <paramref name="ignoreMessageTypes"/> skips message types that would otherwise
    /// hold the session open until timeout (e.g. background jobs scheduled minutes into the future
    /// that the session would never see complete).
    /// </summary>
    private TrackedSessionConfiguration BuildSession(
        bool includeExternalTransports,
        Func<Type, bool>? ignoreMessageTypes = null
    )
    {
        // The session timeout hard-cancels the tracked action (Wolverine wraps the
        // execution in WaitAsync(Timeout)) and throws TimeoutException with the activity
        // grid if activity never completes. Aligned with TestTimeout so nothing hangs.
        var session = Factory.Services.TrackActivity().Timeout(TestTimeout);

        if (includeExternalTransports)
        {
            session = session.IncludeExternalTransports();
        }

        if (ignoreMessageTypes is not null)
        {
            session = session.IgnoreMessagesMatchingType(ignoreMessageTypes);
        }

        return session;
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
        where TMessage : class, BuildingBlocks.Core.Messages.IMessage
    {
        await ExecuteScopeAsync(async sp =>
        {
            var bus = sp.GetRequiredService<IExternalEventBus>();
            await bus.PublishAsync(message, cancellationToken);
        });
    }

    public async ValueTask PublishMessageAsync<TMessage>(
        MessageEnvelope<TMessage> messageEnvelope,
        CancellationToken cancellationToken = default
    )
        where TMessage : class, BuildingBlocks.Core.Messages.IMessage
    {
        await ExecuteScopeAsync(async sp =>
        {
            var bus = sp.GetRequiredService<IExternalEventBus>();
            await bus.PublishAsync(messageEnvelope, cancellationToken);
        });
    }

    public async ValueTask WaitUntilConditionMet(
        Func<Task<bool>> conditionToMet,
        int? timeoutSecond = null,
        string? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        // Cap the infrastructure wait at 90s by default so a stuck test fails fast
        // with a timeout exception instead of hanging the whole run. The caller's
        // cancellation token (e.g. the test-level 90s token) is honored too.
        var time = timeoutSecond ?? 90;

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

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(100, cancellationToken);
            meet = await conditionToMet.Invoke();
            timeoutExpired = DateTime.Now - startTime > TimeSpan.FromSeconds(time);
        }
    }
}
