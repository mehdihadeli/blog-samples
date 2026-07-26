using BuildingBlocks.Abstractions.Messages;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;

namespace ECommerce.Services.Orders.Products.Features.ConsumingProductCreated.v1;

public static class ProductCreatedFaultyHandler
{
    public static class MessageTypes
    {
        public const string FaultyProductCreated = nameof(FaultyProductCreated);
    }

    public static Task Handle(
        MessageEnvelope<ProductCreatedV1> envelope,
        CancellationToken cancellationToken
    )
    {
        if (
            string.Equals(
                envelope.Message.Code,
                MessageTypes.FaultyProductCreated,
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidOperationException(
                "Simulated product-created handling failure for dead-letter queue testing."
            );
        }

        return Task.CompletedTask;
    }
}
