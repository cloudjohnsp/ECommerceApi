using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class InventoryReservationConfiguration
    : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> builder)
    {
        builder.ToTable("inventory_reservations", table =>
        {
            table.HasCheckConstraint(
                "ck_inventory_reservations_quantity",
                "quantity > 0");
            table.HasCheckConstraint(
                "ck_inventory_reservations_status",
                "status IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "ck_inventory_reservations_expiration",
                "expires_at > created_at");
            table.HasCheckConstraint(
                "ck_inventory_reservations_completion",
                "(status = 1 AND completed_at IS NULL) OR " +
                "(status <> 1 AND completed_at IS NOT NULL AND completed_at >= created_at)");
        });

        builder.HasKey(reservation => reservation.Id);
        builder.Property(reservation => reservation.Id).ValueGeneratedNever();
        builder.Property(reservation => reservation.OrderId)
            .HasColumnName("order_id").IsRequired();
        builder.Property(reservation => reservation.ProductId)
            .HasColumnName("product_id").IsRequired();
        builder.Property(reservation => reservation.InventoryId)
            .HasColumnName("inventory_id").IsRequired();
        builder.Property(reservation => reservation.Quantity)
            .HasColumnName("quantity").IsRequired();
        builder.Property(reservation => reservation.Status)
            .HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(reservation => reservation.CreatedAt)
            .HasColumnName("created_at").IsRequired();
        builder.Property(reservation => reservation.ExpiresAt)
            .HasColumnName("expires_at").IsRequired();
        builder.Property(reservation => reservation.CompletedAt)
            .HasColumnName("completed_at");

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(reservation => reservation.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(reservation => reservation.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Inventory>()
            .WithMany()
            .HasForeignKey(reservation => reservation.InventoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(reservation => new { reservation.OrderId, reservation.ProductId })
            .IsUnique()
            .HasDatabaseName("ux_inventory_reservations_order_id_product_id");
        builder.HasIndex(reservation => reservation.InventoryId)
            .HasDatabaseName("ix_inventory_reservations_inventory_id");
        builder.HasIndex(reservation => reservation.ExpiresAt)
            .HasFilter("status = 1")
            .HasDatabaseName("ix_inventory_reservations_active_expires_at");
    }
}
