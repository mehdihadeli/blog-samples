using BuildingBlocks.Core.Messages;

namespace ECommerce.Services.Shared.Contracts.InternalCommands;

public sealed record ProjectProductReadModel(
    Guid ProductId,
    string Code,
    string Name,
    decimal Price,
    DateTime CreatedAtUtc
) : IInternalCommand
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime Created { get; init; } = DateTime.UtcNow;
}
