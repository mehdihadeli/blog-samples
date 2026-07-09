using BuildingBlocks.Integration.Wolverine.Abstractions;
using ECommerce.Services.Catalogs.Products.Models;
using ECommerce.Services.Catalogs.Shared.Data;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.InternalCommands;
using ECommerce.Services.Shared.Contracts.MessageEnvelope;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine.EntityFrameworkCore;

namespace ECommerce.Services.Catalogs.Products.Features.CreatingProduct.v1;

internal static class CreateProductEndpoint
{
    internal static RouteHandlerBuilder MapCreateProductEndpoint(
        this IEndpointRouteBuilder endpoints
    )
    {
        return endpoints.MapPost("/products", Handle).WithName("CreateProduct");
    }

    private static async Task<CreatedAtRoute<CreateProductResponse>> Handle(
        [FromBody] CreateProductRequest request,
        CatalogsDbContext dbContext,
        IDbContextOutbox outbox,
        IExternalEventBus externalEventBus,
        IMessagePersistenceService messagePersistence,
        CancellationToken cancellationToken
    )
    {
        var product = Product.Create(request.Code, request.Name, request.Price);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );

        outbox.Enroll(dbContext);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        var integrationEvent = MessageEnvelope.Create(
            new ProductCreatedV1(
                product.Id,
                product.Code,
                product.Name,
                product.Price,
                product.CreatedAtUtc
            )
        );

        await externalEventBus.PublishAsync(integrationEvent, cancellationToken);
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

        await transaction.CommitAsync(cancellationToken);
        await outbox.FlushOutgoingMessagesAsync();

        return TypedResults.CreatedAtRoute(
            new CreateProductResponse(product.Id, product.Code, product.Name, product.Price),
            "GetProductById",
            new { id = product.Id }
        );
    }
}

internal sealed record CreateProductRequest(string Code, string Name, decimal Price);

internal sealed record CreateProductResponse(Guid Id, string Code, string Name, decimal Price);
