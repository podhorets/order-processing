using OrderService.Domain.Enums;

namespace OrderService.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string MessageType { get; init; } = null!;
    public string Payload { get; init; } = null!;
    public DateTime OccurredAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
    public OutboxStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
}
