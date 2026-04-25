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
        
        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(i => i.Sku).HasMaxLength(100).IsRequired();
        builder.Property(i => i.OnHand).IsRequired();
        builder.Property(x => x.Reserved).IsRequired().HasDefaultValue(0);

        builder.Ignore(x => x.Available); 
        
        builder.HasIndex(x => x.Sku).IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_inventory_onhand_nonneg", "\"OnHand\" >= 0");
            t.HasCheckConstraint("ck_inventory_reserved_nonneg", "\"Reserved\" >= 0");
            t.HasCheckConstraint("ck_inventory_reserved_le_onhand", "\"Reserved\" <= \"OnHand\"");
        });
    }
}
