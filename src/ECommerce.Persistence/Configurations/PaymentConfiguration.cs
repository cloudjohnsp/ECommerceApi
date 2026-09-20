using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(payment => payment.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(payment => payment.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(payment => payment.Provider).HasColumnName("provider").HasMaxLength(100).IsRequired();
        builder.Property(payment => payment.ExternalPaymentId).HasColumnName("external_payment_id").HasMaxLength(200);
        builder.Property(payment => payment.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(payment => payment.PaidAt).HasColumnName("paid_at");
        builder.Property(payment => payment.FailedAt).HasColumnName("failed_at");

        builder.HasOne<Order>()
            .WithOne()
            .HasForeignKey<Payment>(payment => payment.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(payment => payment.OrderId).IsUnique();
        builder.HasIndex(payment => payment.ExternalPaymentId).IsUnique();
    }
}
