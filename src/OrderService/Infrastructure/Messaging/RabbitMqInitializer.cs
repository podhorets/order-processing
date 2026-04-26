using RabbitMQ.Client;
using Shared.Contracts;

namespace OrderService.Infrastructure.Messaging;

public sealed class RabbitMqInitializer(IConnectionFactory factory) : IHostedService
{
    private const string DeadLetterExchange = "dead-letter";

    public async Task StartAsync(CancellationToken ct)
    {
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct,
            durable: true, autoDelete: false, cancellationToken: ct);

        foreach (var queue in MessagingQueues.All)
        {
            var deadQueue = $"{queue}.dead";

            await channel.QueueDeclareAsync(deadQueue, durable: true, exclusive: false,
                autoDelete: false, cancellationToken: ct);

            await channel.QueueBindAsync(deadQueue, DeadLetterExchange, routingKey: queue,
                cancellationToken: ct);

            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"]     = DeadLetterExchange,
                    ["x-dead-letter-routing-key"]  = queue
                },
                cancellationToken: ct);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
