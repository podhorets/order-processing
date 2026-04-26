namespace OrderService.Domain.ValueObjects;

public sealed record OrderItemDraft(string Sku, int Quantity, decimal UnitPrice);
