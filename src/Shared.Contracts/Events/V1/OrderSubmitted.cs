using Shared.Contracts.Dto.V1;

namespace Shared.Contracts.Events.V1;

public sealed record OrderSubmitted(
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyList<OrderItemDto> OrderItems);