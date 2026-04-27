using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Infrastructure.Messaging.Inbox;

namespace OrderService.Infrastructure.Persistence.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");
        builder.HasKey(x => x.MessageId);
        builder.Property(x => x.MessageId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ConsumedAt).IsRequired();
    }
}
