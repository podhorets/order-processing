using UUIDNext;

namespace InventoryService.Domain.Entities;

public class Reservation
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Sku { get; private set; } = null!;
    public int Quantity { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Reservation() { }

    public Reservation(Guid orderId, string sku, int quantity)
    {
        Id        = Uuid.NewSequential();
        OrderId   = orderId;
        Sku       = sku;
        Quantity  = quantity;
        CreatedAt = DateTime.UtcNow;
    }
}
