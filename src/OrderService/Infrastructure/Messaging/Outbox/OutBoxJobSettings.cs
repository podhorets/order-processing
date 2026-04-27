namespace OrderService.Infrastructure.Messaging.Outbox;

public sealed class OutboxSettings
{
    public int BatchSize { get; init; }
    public int MaxRetries { get; init; }
    public TimeSpan FallbackPollInterval { get; init; }
}
