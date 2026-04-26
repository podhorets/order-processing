namespace OrderService.Infrastructure.Messaging;

public interface IProcessOutboxMessagesJob
{
    Task ProcessAsync(CancellationToken ct);
}