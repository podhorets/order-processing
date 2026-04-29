namespace OrderService.Features.SubmitOrder;

public sealed record SubmitOrderRequest(Guid CustomerId, List<SubmitOrderItem> Items);
public sealed record SubmitOrderItem(string Sku, int Quantity, decimal UnitPrice);
