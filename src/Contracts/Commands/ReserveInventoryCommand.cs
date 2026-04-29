using Contracts.Dto;

namespace Contracts.Commands;

public record ReserveInventoryCommand(Guid OrderId, IReadOnlyList<OrderItemDto> Items);
