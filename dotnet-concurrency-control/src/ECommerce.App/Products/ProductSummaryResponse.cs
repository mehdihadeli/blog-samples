namespace ECommerce.Products;

// Lightweight response DTO — positioned at area root, same pattern as Catalogs.
public sealed record ProductSummaryResponse(
    Guid Id,
    string Name,
    int Stock,
    int Version,
    decimal Price
);
