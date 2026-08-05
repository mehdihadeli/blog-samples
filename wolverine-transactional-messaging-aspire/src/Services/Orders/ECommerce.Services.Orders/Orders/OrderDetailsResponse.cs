namespace ECommerce.Services.Orders.Orders;

public sealed record OrderDetailsResponse(Guid Id, string Status, decimal TotalAmount);
