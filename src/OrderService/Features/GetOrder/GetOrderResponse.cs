namespace OrderService.Features.GetOrder;

public sealed record GetOrderResponse(Guid OrderId, Guid CustomerId, string Status, decimal TotalAmount);
