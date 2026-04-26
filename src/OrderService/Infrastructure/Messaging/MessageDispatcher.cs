using System.Text.Json;
using OrderService.Features.ProcessOrder;
using OrderService.Features.RejectOrder;
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

            case MessagingQueues.InventoryReserved:
                await HandleAsync<InventoryReserved, InventoryReservedHandler>(json, ct);
                break;

            case MessagingQueues.InventoryReservationFailed:
                await HandleAsync<InventoryReservationFailed, InventoryReservationFailedHandler>(json, ct);
                break;

            case MessagingQueues.PerformPayment:
                await HandleAsync<PerformPayment, PerformPaymentHandler>(json, ct);
                break;

            case MessagingQueues.PaymentSuccessful:
                await HandleAsync<PaymentSuccessful, PaymentSuccessfulHandler>(json, ct);
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
