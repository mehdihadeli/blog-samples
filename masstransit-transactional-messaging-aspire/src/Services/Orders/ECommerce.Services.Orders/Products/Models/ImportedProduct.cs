namespace ECommerce.Services.Orders.Products.Models;

public class ImportedProduct
{
    private ImportedProduct() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static ImportedProduct Create(
        Guid id,
        string code,
        string name,
        decimal price,
        DateTime createdAtUtc
    )
    {
        return new ImportedProduct
        {
            Id = id,
            Code = code,
            Name = name,
            Price = price,
            CreatedAtUtc = createdAtUtc,
        };
    }

    public void Update(string code, string name, decimal price, DateTime createdAtUtc)
    {
        Code = code;
        Name = name;
        Price = price;
        CreatedAtUtc = createdAtUtc;
    }
}
