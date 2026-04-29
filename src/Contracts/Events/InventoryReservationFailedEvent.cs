namespace Contracts.Events;

public record InventoryReservationFailedEvent(Guid OrderId, string Reason);
