using InventoryService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Infrastructure.Persistence.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReservedAt).IsRequired();
        
        builder.HasIndex(x => new { x.OrderId, x.Sku }).IsUnique();
    }
}
