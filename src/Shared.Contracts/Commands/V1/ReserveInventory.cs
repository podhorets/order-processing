using Shared.Contracts.Dto.V1;

namespace Shared.Contracts.Commands.V1;

public sealed record ReserveInventory(
    Guid OrderId,
    IReadOnlyList<OrderItemDto> OrderItems);