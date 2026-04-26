using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Infrastructure.Messaging.Outbox;

namespace OrderService.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.MessageType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.ProcessedAt);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.RetryCount).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.Error).HasMaxLength(1000);

        builder.HasIndex(x => new { x.Status, x.OccurredAt })
            .HasFilter("\"Status\" = 'Pending'")
            .HasDatabaseName("idx_outbox_messages_pending");
    }
}
