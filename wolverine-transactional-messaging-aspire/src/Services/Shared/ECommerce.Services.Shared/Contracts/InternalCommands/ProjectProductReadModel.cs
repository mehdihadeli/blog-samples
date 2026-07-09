using BuildingBlocks.Abstractions.Messages;

namespace ECommerce.Services.Shared.Contracts.InternalCommands;

public sealed record ProjectProductReadModel(
    Guid ProductId,
    string Code,
    string Name,
    decimal Price,
    DateTime CreatedAtUtc
) : IInternalCommand;
