namespace OrderService.Features.GetOrder;

public sealed record GetOrderResponse(
    Guid OrderId,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    string? RejectionReason,
    DateTime CreatedAt,
    DateTime? CompletedAt);
