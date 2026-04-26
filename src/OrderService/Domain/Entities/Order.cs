using OrderService.Domain.Common;
using OrderService.Domain.Enums;
using OrderService.Domain.ValueObjects;
using UUIDNext;

namespace OrderService.Domain.Entities;

public class Order : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal DiscountApplied { get; private set; }
    public OrderStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    private readonly List<OrderItem> _items = [];
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    private Order() { }
    
    public static Order Submit(Guid customerId, IReadOnlyList<OrderItemDraft> items)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId must not be empty.", nameof(customerId));
        if (items.Count == 0)
            throw new ArgumentException("CustomerId must not be empty.", nameof(items));

        var duplicates = items.GroupBy(l => l.Sku, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
            throw new ArgumentException($"Duplicate SKUs in submission: {string.Join(", ", duplicates)}", nameof(items));

        var order = new Order
        {
            Id = Uuid.NewSequential(),
            CustomerId = customerId,
            Status = OrderStatus.Pending
        };
        
        foreach (var (sku, quantity, price) in items)
            order._items.Add(new OrderItem(sku, quantity, price));
        
        order.RecalculateTotalAmount();
        return order;
    }

    public void MarkProcessing()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot transition from {Status} to Processing");
        Status = OrderStatus.Processing;
    }

    public void MarkProcessed()
    {
        if (Status != OrderStatus.Processing)
            throw new InvalidOperationException($"Cannot transition from {Status} to Processed");

        Status = OrderStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void Reject(string reason)
    {
        if (Status is OrderStatus.Processed or OrderStatus.Rejected)
            throw new InvalidOperationException($"Cannot reject from terminal state {Status}");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        
        Status = OrderStatus.Rejected;
        RejectionReason = reason;
    }

    private void RecalculateTotalAmount()
    {
        var itemsTotal = _items.Sum(i => i.LineTotal);
        TotalAmount = Math.Max(0, itemsTotal - DiscountApplied);
    }
}
