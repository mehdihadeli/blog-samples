using ModelContextProtocol.Server;

namespace Mcp.Catalog;

/// <summary>
/// Product catalog tool server. Behind the gateway the tools are exposed as
/// <c>catalog_*</c>.
/// </summary>
[McpServerToolType]
public sealed class CatalogTools
{
    private static readonly List<Product> Products =
    [
        new("SKU-1001", "Laptop Pro 14", "electronics", 1299.99m, 12),
        new("SKU-1002", "Wireless Mouse", "electronics", 29.99m, 340),
        new("SKU-1003", "Mechanical Keyboard", "electronics", 89.99m, 75),
        new("SKU-2001", "Standing Desk", "furniture", 449.00m, 8),
        new("SKU-2002", "Ergonomic Chair", "furniture", 319.00m, 21),
    ];

    /// <summary>
    /// Search the product catalog by keyword or category.
    /// </summary>
    [McpServerTool(Name = "catalog_search")]
    public IReadOnlyList<Product> Search(string? keyword = null, string? category = null)
    {
        var results = Products.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            results = results.Where(p => p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            results = results.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        return results.ToList();
    }

    /// <summary>
    /// Check the current stock level for a product SKU.
    /// </summary>
    [McpServerTool(Name = "catalog_stock")]
    public Product? Stock(string sku)
    {
        return Products.FirstOrDefault(p => p.Sku == sku);
    }
}

/// <summary>
/// A product in the catalog.
/// </summary>
public sealed class Product
{
    public Product(string sku, string name, string category, decimal price, int stock)
    {
        Sku = sku;
        Name = name;
        Category = category;
        Price = price;
        Stock = stock;
    }

    public string Sku { get; init; }
    public string Name { get; init; }
    public string Category { get; init; }
    public decimal Price { get; init; }
    public int Stock { get; init; }
}