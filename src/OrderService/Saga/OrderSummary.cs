namespace OrderService.Saga;

/// <summary>
/// Persistent read model for the order. Written by OrderSaga on each state transition.
/// Unlike the saga row (deleted on MarkCompleted), this persists forever for GetOrder queries.
/// </summary>
public class OrderSummary
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = null!;
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
