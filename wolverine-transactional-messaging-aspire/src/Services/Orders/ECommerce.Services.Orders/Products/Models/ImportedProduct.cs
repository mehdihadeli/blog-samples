namespace ECommerce.Services.Orders.Products.Models;

public sealed class ImportedProduct
{
    private ImportedProduct() { }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public decimal Price { get; private set; }

    public DateTime SourceCreatedAtUtc { get; private set; }

    public DateTime ReceivedAtUtc { get; private set; }

    public static ImportedProduct Create(
        Guid id,
        string code,
        string name,
        decimal price,
        DateTime sourceCreatedAtUtc
    )
    {
        return new ImportedProduct
        {
            Id = id,
            Code = code,
            Name = name,
            Price = price,
            SourceCreatedAtUtc = sourceCreatedAtUtc,
            ReceivedAtUtc = DateTime.UtcNow,
        };
    }

    public void Update(string code, string name, decimal price, DateTime sourceCreatedAtUtc)
    {
        Code = code;
        Name = name;
        Price = price;
        SourceCreatedAtUtc = sourceCreatedAtUtc;
        ReceivedAtUtc = DateTime.UtcNow;
    }
}
