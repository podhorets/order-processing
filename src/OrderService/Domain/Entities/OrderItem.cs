using OrderService.Domain.Common;
using UUIDNext;

namespace OrderService.Domain.Entities;


public class OrderItem : AuditableEntity
{
    public Guid Id { get; private set; }
    public string Sku { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;

    public Guid OrderId { get; private set; }
    public Order Order { get; private set; } = null!;

    private OrderItem() { }

    internal OrderItem(string sku, int quantity, decimal unitPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

        Id = Uuid.NewSequential();
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
