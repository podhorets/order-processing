namespace OrderService.Infrastructure.Messaging;

public interface IMessageHandler<in T>
{
    Task HandleAsync(T message, CancellationToken ct);
}