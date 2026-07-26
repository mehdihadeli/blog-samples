using BuildingBlocks.Abstractions.Messages;

namespace ECommerce.Services.Catalogs.Products.Features.SyncProductToExternalSystem;

internal sealed record SyncProductToExternalSystem(Guid ProductId) : IInternalCommand
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime Created { get; init; } = DateTime.UtcNow;
}
