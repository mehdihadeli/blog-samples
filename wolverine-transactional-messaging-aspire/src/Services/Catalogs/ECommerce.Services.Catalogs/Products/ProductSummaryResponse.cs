namespace ECommerce.Services.Catalogs.Products;

public sealed record ProductSummaryResponse(Guid Id, string Code, string Name, decimal Price);
