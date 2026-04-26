namespace OrderService.Features.SubmitOrder;

public sealed record SubmitOrderResponse(Guid OrderId, string Status);