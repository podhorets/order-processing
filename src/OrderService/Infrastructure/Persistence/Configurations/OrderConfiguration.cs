using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(x => x.CustomerId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.TotalAmount).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.DiscountApplied).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.ProcessedAt);

        builder.HasIndex(x => x.CustomerId);
        
        builder.HasMany(b => b.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
