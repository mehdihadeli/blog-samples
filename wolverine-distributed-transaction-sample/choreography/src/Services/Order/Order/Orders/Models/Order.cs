namespace Order.Orders.Models;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Cancelled,
    TimedOut,
}

public sealed class Order
{
    public Guid Id { get; private set; }
    public string CustomerName { get; private set; } = null!;
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Order() { } // EF Core

    public static Order Create(string customerName, decimal total)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = customerName,
            Total = total,
            Status = OrderStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot confirm order {Id}: current status is {Status}."
            );
        Status = OrderStatus.Confirmed;
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Confirmed)
            throw new InvalidOperationException($"Cannot cancel order {Id}: already confirmed.");
        Status = reason == "timeout" ? OrderStatus.TimedOut : OrderStatus.Cancelled;
    }
}
