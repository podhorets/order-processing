namespace OrderService.Infrastructure.Messaging.Outbox;

public interface IProcessOutboxMessagesJob
{
    Task ProcessAsync(CancellationToken ct);
}
