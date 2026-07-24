namespace ECommerce.Services.Catalogs.Products.Dtos.v1;

public sealed record ProductDto(
    Guid Id,
    string Code,
    string Name,
    decimal Price,
    DateTime CreatedAtUtc
);
