using BuildingBlocks.Core.Messages;

namespace ECommerce.BuildingBlocks.TestHost.Messages;

// Test-only integration events. Deliberately defined HERE (not in any
// microservice) so the building-block integration tests stay fully
// independent from the e-commerce services.

public sealed record ProductCreatedV1(
    Guid ProductId,
    string Code,
    string Name,
    decimal Price,
    DateTime CreatedAtUtc
) : IIntegrationEvent
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime Created { get; init; } = DateTime.UtcNow;
}

public sealed record OrderCreatedV1(Guid OrderId, Guid CustomerId, decimal TotalAmount)
    : IIntegrationEvent
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime Created { get; init; } = DateTime.UtcNow;
}

public sealed record InventoryAdjustedV1(Guid ProductId, int QuantityChange) : IIntegrationEvent
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime Created { get; init; } = DateTime.UtcNow;
}
