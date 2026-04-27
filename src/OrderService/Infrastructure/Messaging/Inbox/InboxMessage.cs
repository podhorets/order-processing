namespace OrderService.Infrastructure.Messaging.Inbox;

public sealed class InboxMessage
{
    public string MessageId { get; init; } = null!;
    public DateTime ConsumedAt { get; init; }
}
