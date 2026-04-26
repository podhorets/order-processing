namespace OrderService.Infrastructure.Messaging.Outbox;

public enum OutboxStatus
{
    Pending,
    Processed,
    Failed
}
