using System.Threading.Channels;

namespace OrderService.Infrastructure.Messaging.Outbox;

public sealed class OutboxChannel
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Signal() => _channel.Writer.TryWrite(true);

    public ValueTask<bool> WaitAsync(CancellationToken ct) => _channel.Reader.ReadAsync(ct);
}
