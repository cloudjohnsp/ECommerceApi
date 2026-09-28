using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasColumnName("Type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnName("Payload")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("UpdatedAt");

        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ProcessedAt);
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.NextAttemptAt).IsRequired();
        builder.Property(x => x.LockId);
        builder.Property(x => x.LockedUntil);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();

        builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt })
            .HasDatabaseName("IX_OutboxMessages_PendingDispatch")
            .HasFilter("\"Status\" = 1");
    }
}
