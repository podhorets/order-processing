using UUIDNext;

namespace PaymentService.Domain.Entities;

public class PaymentRecord
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTime ProcessedAt { get; private set; }

    private PaymentRecord() { }

    public PaymentRecord(Guid orderId, Guid customerId, decimal amount, string status)
    {
        Id          = Uuid.NewSequential();
        OrderId     = orderId;
        CustomerId  = customerId;
        Amount      = amount;
        Status      = status;
        ProcessedAt = DateTime.UtcNow;
    }
}
