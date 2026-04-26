using System.Text.Json;
using OrderService.Features.ReleaseInventory;
using OrderService.Features.ReserveInventory;
using Shared.Contracts;
using Shared.Contracts.Commands.V1;
using Shared.Contracts.Events.V1;

namespace OrderService.Infrastructure.Messaging;

public sealed class MessageDispatcher(IServiceScopeFactory scopeFactory) : IMessageDispatcher
{
    public async Task DispatchAsync(string messageType, string json, CancellationToken ct)
    {
        switch (messageType)
        {
            case MessagingQueues.OrderSubmitted:
                await HandleAsync<OrderSubmitted, OrderSubmittedHandler>(json, ct);
                break;

            case MessagingQueues.InventoryReleased:
                await HandleAsync<ReleaseInventory, ReleaseInventoryHandler>(json, ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown message type: '{messageType}'.");
        }
    }

    private async Task HandleAsync<T, THandler>(string json, CancellationToken ct)
        where THandler : IMessageHandler<T>
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var message = JsonSerializer.Deserialize<T>(json)!;
        var handler = scope.ServiceProvider.GetRequiredService<THandler>();
        await handler.HandleAsync(message, ct);
    }
}
