using UUIDNext;

namespace InventoryService.Domain.Entities;

public class Inventory
{
    public Guid Id { get; private set; }
    public string Sku { get; private set; } = null!;
    public int OnHand { get; private set; }
    public int Reserved { get; private set; }
    public int Available => OnHand - Reserved;

    private Inventory() { }

    public Inventory(string sku, int onHand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegative(onHand);
        Id    = Uuid.NewSequential();
        Sku   = sku;
        OnHand = onHand;
    }

    public void Reserve(int qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);
        if (Available < qty)
            throw new InvalidOperationException($"Insufficient stock for '{Sku}': requested {qty}, available {Available}");
        Reserved += qty;
    }

    public void Release(int qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);
        Reserved = Math.Max(0, Reserved - qty);
    }

    public void Fulfill(int qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);
        if (OnHand < qty)
            throw new InvalidOperationException($"Cannot fulfill more than OnHand for '{Sku}'");
        OnHand   -= qty;
        Reserved -= qty;
    }

    public void AddStock(int qty)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qty);
        OnHand += qty;
    }
}
