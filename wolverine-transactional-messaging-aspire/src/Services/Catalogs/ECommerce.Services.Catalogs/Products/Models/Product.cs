namespace ECommerce.Services.Catalogs.Products.Models;

public sealed class Product
{
    private Product() { }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public decimal Price { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static Product Create(string code, string name, decimal price)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Price = price,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
