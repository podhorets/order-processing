namespace OrderService.Contracts.Commands.V1;

public sealed record PerformPayment(Guid OrderId, Guid CustomerId, decimal TotalAmount);
