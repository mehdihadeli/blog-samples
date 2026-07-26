using BuildingBlocks.Abstractions.Messages;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;

namespace Tests.Shared;

public static class SampleData
{
    public static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly DateTime CreatedAtUtc = new(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc);

    public static MessageEnvelope<ProductCreatedV1> ProductCreatedEnvelope(
        Guid? correlationId = null,
        Guid? messageId = null
    )
    {
        return MessageEnvelopeFactory.From(
            new ProductCreatedV1(ProductId, "catalog-001", "Starter Basket", 42.50m, CreatedAtUtc),
            correlationId ?? Guid.NewGuid(),
            causationId: null
        );
    }
}
