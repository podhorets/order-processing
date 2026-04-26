using RabbitMQ.Client;
using Shared.Contracts;

namespace OrderService.Infrastructure.Messaging;

public sealed class RabbitMqInitializer(IConnectionFactory factory) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        foreach (var queue in MessagingQueues.All)
        {
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false,
                autoDelete: false, cancellationToken: ct);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
