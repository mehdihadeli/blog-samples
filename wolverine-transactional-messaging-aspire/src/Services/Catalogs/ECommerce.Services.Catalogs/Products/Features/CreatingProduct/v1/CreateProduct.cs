using BuildingBlocks.Abstractions.Messages;
using ECommerce.Services.Catalogs.Products.Models;
using ECommerce.Services.Catalogs.Shared.Data;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.InternalCommands;
using MediatR;
using Wolverine.EntityFrameworkCore;

namespace ECommerce.Services.Catalogs.Products.Features.CreatingProduct.v1;

internal sealed record CreateProduct(string Code, string Name, decimal Price)
    : IRequest<CreateProductResult>;

internal sealed record CreateProductResult(Guid Id, string Code, string Name, decimal Price);

internal sealed class CreateProductHandler(
    CatalogsDbContext dbContext,
    IDbContextOutbox outbox,
    IExternalEventBus externalEventBus,
    IMessagePersistenceService messagePersistence,
    IBackgroundJobScheduler jobScheduler
) : IRequestHandler<CreateProduct, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(
        CreateProduct command,
        CancellationToken cancellationToken
    )
    {
        var product = Product.Create(command.Code, command.Name, command.Price);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );

        outbox.Enroll(dbContext);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        await externalEventBus.PublishAsync(
            new ProductCreatedV1(
                product.Id,
                product.Code,
                product.Name,
                product.Price,
                product.CreatedAtUtc
            ),
            cancellationToken
        );

        await messagePersistence.EnqueueLocalAsync(
            new ProjectProductReadModel(
                product.Id,
                product.Code,
                product.Name,
                product.Price,
                product.CreatedAtUtc
            ),
            cancellationToken
        );

        // Schedule a background job to sync to external CRM, 5 minutes after creation.
        await jobScheduler.ScheduleAsync(
            new SyncProductToExternalSystem.SyncProductToExternalSystem(product.Id),
            TimeSpan.FromMinutes(5),
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);
        await outbox.FlushOutgoingMessagesAsync();

        return new CreateProductResult(product.Id, product.Code, product.Name, product.Price);
    }
}
