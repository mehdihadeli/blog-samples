namespace ECommerce.Orders.Models;

public sealed class Order
{
    private Order() { }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TotalPrice => Quantity * UnitPrice;
    public string Status { get; private set; } = "Pending";
    public string? ConcurrencyStrategy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Order Create(
        Guid productId,
        string productName,
        int quantity,
        decimal unitPrice,
        string concurrencyStrategy
    )
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Status = "Confirmed",
            ConcurrencyStrategy = concurrencyStrategy,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
