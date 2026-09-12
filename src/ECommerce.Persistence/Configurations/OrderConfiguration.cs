using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();
        builder.Property(order => order.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(order => order.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(order => order.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(order => order.UpdatedAt).HasColumnName("updated_at");
        builder.Property(order => order.CancelledAt).HasColumnName("cancelled_at");
        builder.Ignore(order => order.Total);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(order => order.Items)
            .WithOne()
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(order => order.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(order => order.CustomerId);
        builder.HasIndex(order => order.CreatedAt);
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(item => item.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(item => item.ProductName).HasColumnName("product_name").HasMaxLength(150).IsRequired();
        builder.Property(item => item.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2).IsRequired();
        builder.Property(item => item.Quantity).HasColumnName("quantity").IsRequired();
        builder.Ignore(item => item.Subtotal);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.OrderId);
        builder.HasIndex(item => item.ProductId);
    }
}
