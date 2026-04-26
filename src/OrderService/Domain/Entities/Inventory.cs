using OrderService.Domain.Common;
using UUIDNext;

namespace OrderService.Domain.Entities;

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

    public void Reserve(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (Available < quantity)
            throw new InvalidOperationException($"Insufficient stock for {Sku}.");

        Reserved += quantity;
    }

    public void Fulfill(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        OnHand   -= quantity;
        Reserved -= quantity;
    }
}
