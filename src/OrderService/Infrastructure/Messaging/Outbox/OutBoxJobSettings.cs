namespace OrderService.Infrastructure.Messaging.Outbox;

public sealed class OutBoxJobSettings
{
    public string Schedule { get; init; } = "* * * * *";
    public int BatchSize { get; init; } = 50;
    public int MaxRetries { get; init; } = 3;
}
