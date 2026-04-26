using OrderService.Contracts.Dto.V1;

namespace OrderService.Features.SubmitOrder;

public sealed record SubmitOrderRequest(Guid CustomerId, IReadOnlyList<OrderItemDto> OrderItems);
