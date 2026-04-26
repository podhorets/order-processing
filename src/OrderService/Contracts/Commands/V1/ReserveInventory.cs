using OrderService.Contracts.Dto.V1;

namespace OrderService.Contracts.Commands.V1;

public sealed record ReserveInventory(
    Guid OrderId,
    IReadOnlyList<OrderItemDto> OrderItems);
