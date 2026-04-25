using InventoryService.Domain.Common;
using UUIDNext;

namespace InventoryService.Domain.Entities;

public class Inventory : AuditableEntity
{
    public Guid Id { get; private set; }
    public string Sku { get; private set; } = null!;
    public int OnHand   { get; private set; }
    public int Reserved { get; private set; }

    public int Available => OnHand - Reserved;

    private Inventory() { }

    public Inventory(string sku, int onHand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegative(onHand);
        
        Id = Uuid.NewSequential();
        Sku = sku;
        OnHand = onHand;
        Reserved = 0;
    }
}
