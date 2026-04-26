namespace OrderService.Infrastructure.Messaging;

public sealed class OutBoxJobSettings
{
    public string Schedule { get; init; } = "* * * * *";
    public int BatchSize { get; init; } = 50;
    public int MaxRetries { get; init; } = 3;
}
