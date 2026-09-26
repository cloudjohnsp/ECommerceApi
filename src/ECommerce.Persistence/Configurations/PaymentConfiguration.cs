using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", table =>
        {
            table.HasCheckConstraint(
                "ck_payments_currency",
                "\"currency\" ~ '^[A-Z]{3}$'");
            table.HasCheckConstraint("ck_payments_amount", "amount > 0");
            table.HasCheckConstraint("ck_payments_status", "status IN (1, 2, 3, 4)");
        });
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(payment => payment.Amount).HasColumnName("amount")
            .HasPrecision(MoneyConstraints.Precision, MoneyConstraints.Scale).IsRequired();
        builder.Property(payment => payment.Currency).HasColumnName("currency")
            .HasColumnType("character(3)").IsRequired();
        builder.Property(payment => payment.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(payment => payment.Provider).HasColumnName("provider").HasMaxLength(100).IsRequired();
        builder.Property(payment => payment.ExternalPaymentId).HasColumnName("external_payment_id").HasMaxLength(200);
        builder.Property(payment => payment.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(payment => payment.PaidAt).HasColumnName("paid_at");
        builder.Property(payment => payment.FailedAt).HasColumnName("failed_at");
        builder.Property(payment => payment.RefundedAt).HasColumnName("refunded_at");

        builder.HasOne<Order>()
            .WithOne()
            .HasForeignKey<Payment>(payment => payment.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(payment => payment.OrderId).IsUnique();
        builder.HasIndex(payment => payment.ExternalPaymentId).IsUnique();
    }
}
