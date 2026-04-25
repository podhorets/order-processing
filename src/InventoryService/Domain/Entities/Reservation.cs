using InventoryService.Domain.Common;
using InventoryService.Domain.Enums;
using UUIDNext;

namespace InventoryService.Domain.Entities;

public class Reservation : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Sku { get; private set; } = null!;
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset ReservedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }

    private Reservation() { }

    public Reservation(Guid orderId, string sku, int quantity)
    {
        if (orderId == Guid.Empty) 
            throw new ArgumentException("OrderId must not be empty.", nameof(orderId));

        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        
        Id = Uuid.NewSequential();
        OrderId = orderId;
        Sku = sku;
        Quantity = quantity;
        Status = ReservationStatus.Reserved;
        ReservedAt = DateTimeOffset.UtcNow;
    }

    public void MarkReleased()
    {
        if (Status == ReservationStatus.Released) 
            throw new InvalidOperationException($"Reservation {Id} is already released.");
        Status = ReservationStatus.Released;
        ReleasedAt = DateTimeOffset.UtcNow;
    }
}
