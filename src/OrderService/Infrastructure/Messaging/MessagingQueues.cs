namespace OrderService.Infrastructure.Messaging;

public static class MessagingQueues
{
    public const string OrderSubmitted = "order.submitted";
    public const string InventoryReserved = "inventory.reserved";
    public const string InventoryReservationFailed = "inventory.reservation-failed";
    public const string InventoryReleased = "inventory.released";

    public static readonly IReadOnlyList<string> All =
        [OrderSubmitted, InventoryReserved, InventoryReservationFailed, InventoryReleased];
}