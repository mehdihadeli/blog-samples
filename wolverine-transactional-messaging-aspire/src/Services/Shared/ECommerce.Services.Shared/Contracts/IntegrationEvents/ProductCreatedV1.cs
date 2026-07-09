using BuildingBlocks.Abstractions.Messages;

namespace ECommerce.Services.Shared.Contracts.IntegrationEvents;

public sealed record ProductCreatedV1(
    Guid ProductId,
    string Code,
    string Name,
    decimal Price,
    DateTime CreatedAtUtc
) : IIntegrationEvent;
