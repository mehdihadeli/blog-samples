using BuildingBlocks.Integration.Wolverine.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace BuildingBlocks.Integration.Wolverine;

internal sealed class WolverineMessagePersistenceService(
    IBusDirectPublisher busDirectPublisher,
    IMessageBus bus,
    IServiceProvider serviceProvider
) : IMessagePersistenceService
{
    public ValueTask PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryEnrollCurrentDbContext();
        return busDirectPublisher.PublishAsync(message, cancellationToken);
    }

    public ValueTask EnqueueLocalAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default
    )
        where TMessage : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryEnrollCurrentDbContext();
        return bus.SendAsync(message);
    }

    private void TryEnrollCurrentDbContext()
    {
        if (serviceProvider.GetService(typeof(IDbContextOutbox)) is not IDbContextOutbox outbox)
        {
            return;
        }

        var dbContext = serviceProvider
            .GetServices<Microsoft.EntityFrameworkCore.DbContext>()
            .FirstOrDefault();

        if (dbContext is not null)
        {
            outbox.Enroll(dbContext);
        }
    }
}
