using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Domain.Configurations;

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
    }
}
