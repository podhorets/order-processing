namespace OrderService.Contracts.Events.V1;

public sealed record InventoryReservationFailed(Guid OrderId, string Reason);
