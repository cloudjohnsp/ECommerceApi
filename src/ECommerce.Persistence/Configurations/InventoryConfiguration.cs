using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("inventories", table =>
        {
            table.HasCheckConstraint("ck_inventories_stock", "stock >= 0");
            table.HasCheckConstraint("ck_inventories_reserved_stock", "reserved_stock >= 0");
            table.HasCheckConstraint(
                "ck_inventories_reservation_capacity",
                "reserved_stock <= stock");
        });
        builder.HasKey(inventory => inventory.Id);
        builder.Property(inventory => inventory.Id).ValueGeneratedNever();
        builder.Property(inventory => inventory.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property<int>("_stock").HasColumnName("stock").IsRequired();
        builder.Property<int>("_reservedStock").HasColumnName("reserved_stock").IsRequired();
        builder.Ignore(inventory => inventory.AvailableStock);
        builder.HasIndex(inventory => inventory.ProductId).IsUnique();
    }
}
