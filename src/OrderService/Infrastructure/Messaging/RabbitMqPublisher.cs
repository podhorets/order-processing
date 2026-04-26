using System.Text;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Messaging;

public sealed class RabbitMqPublisher(IConnectionFactory factory) : IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_channel is not null) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_channel is not null) return;

            _connection = await factory.CreateConnectionAsync(ct);

            var channelOpts = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
            _channel = await _connection.CreateChannelAsync(channelOpts, ct);
            
            foreach (var queue in new[]
                     {
                         MessagingQueues.OrderSubmitted, MessagingQueues.InventoryReleased,
                         MessagingQueues.InventoryReserved, MessagingQueues.InventoryReservationFailed
                     })
            {
                await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false,
                    autoDelete: false, cancellationToken: ct);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }
    
    public async Task PublishAsync(string queueName, string payload, CancellationToken ct = default)
    {
        var body = Encoding.UTF8.GetBytes(payload);
        var props = new BasicProperties { DeliveryMode = DeliveryModes.Persistent };

        if (_channel is null)
            throw new InvalidOperationException("Channel not initialized.");

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _initLock.Dispose();
    }
}
