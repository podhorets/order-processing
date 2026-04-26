namespace InventoryService.Domain.Common;

public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    internal void SetCreatedAt(DateTime value) => CreatedAt = value;
    internal void SetUpdatedAt(DateTime value) => UpdatedAt = value;
}