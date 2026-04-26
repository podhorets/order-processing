namespace OrderService.Infrastructure.Messaging.Outbox;

public sealed class OutboxSettings
{
    public int BatchSize { get; init; } = 50;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan FallbackPollInterval { get; init; } = TimeSpan.FromSeconds(10);
}
