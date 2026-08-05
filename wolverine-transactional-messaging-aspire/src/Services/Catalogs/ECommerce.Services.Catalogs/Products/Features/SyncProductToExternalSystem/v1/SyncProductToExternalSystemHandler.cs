using Microsoft.Extensions.Logging;

namespace ECommerce.Services.Catalogs.Products.Features.SyncProductToExternalSystem.v1;

internal static class SyncProductToExternalSystemHandler
{
    internal static Task Handle(
        SyncProductToExternalSystem command,
        ILogger<SyncProductToExternalSystem> logger,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Syncing product {ProductId} to external CRM system",
            command.ProductId
        );

        // Simulate external system sync — in production this calls an HTTP API.
        return Task.CompletedTask;
    }
}
