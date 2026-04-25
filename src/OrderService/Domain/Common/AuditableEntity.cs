namespace OrderService.Domain.Common;

public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    internal void SetCreatedAt(DateTimeOffset value) => CreatedAt = value;
    internal void SetUpdatedAt(DateTimeOffset value) => UpdatedAt = value;
}