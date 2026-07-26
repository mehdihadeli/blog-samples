using BuildingBlocks.Abstractions.Messages;
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
    public ValueTask PublishAsync(IMessageEnvelope messageEnvelope, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        TryEnrollCurrentDbContext();
        return busDirectPublisher.PublishAsync(messageEnvelope, ct);
    }

    public ValueTask EnqueueLocalAsync(
        IMessageEnvelope messageEnvelope,
        CancellationToken ct = default
    )
    {
        ct.ThrowIfCancellationRequested();
        TryEnrollCurrentDbContext();
        return bus.SendAsync(messageEnvelope.Message);
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
