using InventoryService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Infrastructure.Persistence.Configurations;

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OnHand).IsRequired();
        builder.Property(x => x.Reserved).IsRequired().HasDefaultValue(0);
        builder.HasIndex(x => x.Sku).IsUnique();

        // PostgreSQL xmin system column — concurrency token, no migration needed.
        // EF adds WHERE xmin = @orig on UPDATE; conflict → DbUpdateConcurrencyException.
        // Wolverine retries the entire handler on this exception.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_inventory_onhand_nonneg",         "\"OnHand\" >= 0");
            t.HasCheckConstraint("ck_inventory_reserved_nonneg",       "\"Reserved\" >= 0");
            t.HasCheckConstraint("ck_inventory_reserved_le_onhand",    "\"Reserved\" <= \"OnHand\"");
        });
    }
}
