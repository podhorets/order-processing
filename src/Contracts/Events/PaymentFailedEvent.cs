namespace Contracts.Events;

public record PaymentFailedEvent(Guid OrderId, string Reason);
