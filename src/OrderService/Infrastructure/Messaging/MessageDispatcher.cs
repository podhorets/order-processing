using System.Text.Json;
using OrderService.Contracts;
using OrderService.Contracts.Commands.V1;
using OrderService.Contracts.Events.V1;
using OrderService.Features.FulfillOrder;
using OrderService.Features.InitiatePayment;
using OrderService.Features.ProcessPayment;
using OrderService.Features.RejectOrder;
using OrderService.Features.ReserveInventory;

namespace OrderService.Infrastructure.Messaging;

public sealed class MessageDispatcher(IServiceScopeFactory scopeFactory) : IMessageDispatcher
{
    public async Task DispatchAsync(string messageType, string json, CancellationToken ct)
    {
        switch (messageType)
        {
            case MessagingQueues.OrderSubmitted:
                await HandleAsync<OrderSubmitted, ReserveInventoryHandler>(json, ct);
                break;

            case MessagingQueues.InventoryReserved:
                await HandleAsync<InventoryReserved, InitiatePaymentHandler>(json, ct);
                break;

            case MessagingQueues.InventoryReservationFailed:
                await HandleAsync<InventoryReservationFailed, RejectOrderHandler>(json, ct);
                break;

            case MessagingQueues.PerformPayment:
                await HandleAsync<PerformPayment, ProcessPaymentHandler>(json, ct);
                break;

            case MessagingQueues.PaymentSuccessful:
                await HandleAsync<PaymentSuccessful, FulfillOrderHandler>(json, ct);
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
