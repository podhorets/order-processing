using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(i => i.OrderId).IsRequired();
        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(i => i.Sku).HasMaxLength(100).IsRequired();

        builder.Ignore(i => i.LineTotal);

        builder.HasIndex(i => new { i.OrderId, i.Sku }).IsUnique();
    }
}
