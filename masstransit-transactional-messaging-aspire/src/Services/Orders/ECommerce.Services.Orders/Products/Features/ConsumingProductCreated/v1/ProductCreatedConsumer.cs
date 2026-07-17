using ECommerce.Services.Orders.Products.Models;
using ECommerce.Services.Orders.Shared.Data;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Orders.Products.Features.ConsumingProductCreated.v1;

public sealed class ProductCreatedConsumer(OrdersDbContext dbContext)
    : IConsumer<MessageEnvelope<ProductCreatedV1>>
{
    public async Task Consume(ConsumeContext<MessageEnvelope<ProductCreatedV1>> context)
    {
        var message = context.Message.Message;
        if (
            string.Equals(
                message.Code,
                "faulty-product-created",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                "Intentional consumer failure for retry and dead-letter tests."
            );
        }

        var existing = await dbContext.ImportedProducts.SingleOrDefaultAsync(
            x => x.Id == message.ProductId,
            context.CancellationToken
        );

        if (existing is null)
        {
            dbContext.ImportedProducts.Add(
                ImportedProduct.Create(
                    message.ProductId,
                    message.Code,
                    message.Name,
                    message.Price,
                    message.CreatedAtUtc
                )
            );
        }
        else
        {
            existing.Update(message.Code, message.Name, message.Price, message.CreatedAtUtc);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
