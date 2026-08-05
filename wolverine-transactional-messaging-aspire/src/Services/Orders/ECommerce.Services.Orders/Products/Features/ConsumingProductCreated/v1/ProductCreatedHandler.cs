using BuildingBlocks.Core.Messages;
using ECommerce.Services.Orders.Products.Models;
using ECommerce.Services.Orders.Shared.Data;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Orders.Products.Features.ConsumingProductCreated.v1;

public static class ProductCreatedHandler
{
    public static async Task Handle(
        MessageEnvelope<ProductCreatedV1> envelope,
        OrdersDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        await ProductCreatedFaultyHandler.Handle(envelope, cancellationToken);

        var message = envelope.Message;
        var existing = await dbContext.ImportedProducts.SingleOrDefaultAsync(
            x => x.Id == message.ProductId,
            cancellationToken
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

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
