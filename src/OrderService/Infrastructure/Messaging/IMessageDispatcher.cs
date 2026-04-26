namespace OrderService.Infrastructure.Messaging;

public interface IMessageDispatcher
{
    Task DispatchAsync(string messageType, string json, CancellationToken ct);
}