using BuildingBlocks.Abstractions.Messages;

namespace ECommerce.Services.Shared.Contracts.IntegrationEvents;

public sealed record OrderSubmittedV1(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime SubmittedAtUtc
) : IIntegrationEvent
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime Created { get; init; } = DateTime.UtcNow;
}
