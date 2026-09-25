using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class UserAuditEntryConfiguration : IEntityTypeConfiguration<UserAuditEntry>
{
    public void Configure(EntityTypeBuilder<UserAuditEntry> builder)
    {
        builder.ToTable("user_audit_entries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(entry => entry.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(entry => entry.Action)
            .HasColumnName("action")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(entry => entry.ChangesJson)
            .HasColumnName("changes")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(entry => entry.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.HasIndex(entry => new { entry.UserId, entry.OccurredAt });
        builder.HasIndex(entry => entry.ActorUserId);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
