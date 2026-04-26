using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;

namespace OrderService.Infrastructure.Messaging;

public sealed class RabbitMqConsumerService(
    IConnectionFactory factory,
    IMessageDispatcher dispatcher,
    ILogger<RabbitMqConsumerService> logger) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        var queues = new[]
        {
            MessagingQueues.OrderSubmitted,
            MessagingQueues.InventoryReservationFailed,
            MessagingQueues.InventoryReleased
        };

        foreach (var queue in queues)
        {
            await _channel.QueueDeclareAsync(queue, true, false, false, cancellationToken: ct);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                    await dispatcher.DispatchAsync(queue, json, ct);

                    await _channel.BasicAckAsync(ea.DeliveryTag, false, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Consumer error");

                    await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true, ct);
                }
            };

            await _channel.BasicConsumeAsync(queue, false, consumer, ct);
        }

        await Task.Delay(Timeout.Infinite, ct);
    }
}