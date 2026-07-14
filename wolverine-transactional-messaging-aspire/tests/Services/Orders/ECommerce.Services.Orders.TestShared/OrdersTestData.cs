using Bogus;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using Tests.Shared;

namespace ECommerce.Services.Orders.TestShared;

public static class OrdersTestData
{
    public static Guid ExistingProductId => SampleData.ProductId;

    private static readonly Faker<ProductCreatedEnvelopeData> ProductCreatedEnvelopeFaker =
        new Faker<ProductCreatedEnvelopeData>().CustomInstantiator(
            faker => new ProductCreatedEnvelopeData(
                Guid.NewGuid(),
                $"catalog-{faker.Random.AlphaNumeric(10).ToLowerInvariant()}",
                $"{faker.Commerce.ProductAdjective()} {faker.Commerce.ProductMaterial()} {faker.Commerce.ProductName()}",
                decimal.Round(faker.Random.Decimal(5, 200), 2),
                faker.Date.RecentOffset(10).UtcDateTime
            )
        );

    public static ProductCreatedEnvelopeData NewProductCreatedEnvelope()
    {
        return ProductCreatedEnvelopeFaker.Generate();
    }
}

public sealed record ProductCreatedEnvelopeData(
    Guid ProductId,
    string Code,
    string Name,
    decimal Price,
    DateTime OccurredAtUtc
)
{
    public MessageEnvelope<ProductCreatedV1> ToEnvelope(
        Guid? correlationId = null,
        Guid? messageId = null
    )
    {
        return MessageEnvelope.Create(
            new ProductCreatedV1(ProductId, Code, Name, Price, OccurredAtUtc),
            correlationId,
            messageId,
            OccurredAtUtc
        );
    }
}
