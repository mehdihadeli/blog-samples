using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;

namespace Tests.Shared;

public static class SampleData
{
    public static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly DateTime CreatedAtUtc = new(2026, 07, 14, 12, 0, 0, DateTimeKind.Utc);

    public static MessageEnvelope<ProductCreatedV1> ProductCreatedEnvelope(
        Guid? correlationId = null,
        Guid? messageId = null
    )
    {
        return MessageEnvelope.Create(
            new ProductCreatedV1(ProductId, "catalog-001", "Starter Basket", 42.50m, CreatedAtUtc),
            correlationId,
            messageId,
            CreatedAtUtc
        );
    }
}
