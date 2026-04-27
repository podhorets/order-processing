using System.Text;
using OrderService.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderService.Infrastructure.Messaging;

public sealed class RabbitMqConsumerService(
    IConnectionFactory factory,
    IMessageDispatcher dispatcher,
    ILogger<RabbitMqConsumerService> logger) : BackgroundService
{
    private const int MaxRetries = 3;
    private const string RetryCountHeader = "x-retry-count";

    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        var queues = new[]
        {
            MessagingQueues.OrderSubmitted,
            MessagingQueues.InventoryReserved,
            MessagingQueues.InventoryReservationFailed,
            MessagingQueues.PerformPayment,
            MessagingQueues.PaymentSuccessful,
            MessagingQueues.InventoryReleased
        };

        foreach (var queue in queues)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var messageId = ea.BasicProperties.MessageId ?? ea.DeliveryTag.ToString();
                    await dispatcher.DispatchAsync(queue, json, messageId, ct);
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, ct);
                }
                catch (Exception ex)
                {
                    var retryCount = GetRetryCount(ea.BasicProperties);

                    if (retryCount < MaxRetries)
                    {
                        logger.LogWarning(ex,
                            "Message processing failed on queue {Queue}, retry {Retry}/{Max}",
                            queue, retryCount + 1, MaxRetries);

                        var headers = new Dictionary<string, object?>(
                            ea.BasicProperties.Headers ?? new Dictionary<string, object?>())
                        {
                            [RetryCountHeader] = retryCount + 1
                        };

                        await _channel.BasicPublishAsync(
                            exchange: "",
                            routingKey: queue,
                            mandatory: false,
                            basicProperties: new BasicProperties { Persistent = true, Headers = headers },
                            body: ea.Body,
                            cancellationToken: ct);

                        await _channel.BasicAckAsync(ea.DeliveryTag, false, ct);
                    }
                    else
                    {
                        logger.LogError(ex,
                            "Message on queue {Queue} exhausted {Max} retries, routing to dead-letter queue",
                            queue, MaxRetries);

                        await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, ct);
                    }
                }
            };

            await _channel.BasicConsumeAsync(queue, false, consumer, ct);
        }

        await Task.Delay(Timeout.Infinite, ct);
    }

    private static int GetRetryCount(IReadOnlyBasicProperties props)
        => props.Headers?.TryGetValue(RetryCountHeader, out var value) == true
            ? Convert.ToInt32(value)
            : 0;
}
