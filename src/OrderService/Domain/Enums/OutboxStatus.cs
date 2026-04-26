namespace OrderService.Domain.Enums;

public enum OutboxStatus
{
    Pending,
    Processed,
    Failed
}