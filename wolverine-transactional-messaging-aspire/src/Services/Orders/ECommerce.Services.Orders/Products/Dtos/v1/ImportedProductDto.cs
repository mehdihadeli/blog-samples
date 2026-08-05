namespace ECommerce.Services.Orders.Products.Dtos.v1;

public sealed record ImportedProductDto(
    Guid Id,
    string Code,
    string Name,
    decimal Price,
    DateTime SourceCreatedAtUtc,
    DateTime ReceivedAtUtc
);
