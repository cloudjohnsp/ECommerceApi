using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", table =>
            table.HasCheckConstraint("ck_products_price", "price > 0"));
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).ValueGeneratedNever();

        builder.Property(product => product.Name)
            .HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(product => product.Description)
            .HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(product => product.Price)
            .HasColumnName("price").HasPrecision(MoneyConstraints.Precision, MoneyConstraints.Scale).IsRequired();
        builder.Property(product => product.CategoryId).HasColumnName("category_id");
        builder.Ignore(product => product.AvailableStock);
        builder.Property(product => product.IsActive)
            .HasColumnName("is_active").IsRequired();
        builder.Property(product => product.CreatedAt)
            .HasColumnName("created_at").IsRequired();
        builder.Property(product => product.UpdatedAt).HasColumnName("updated_at");
        builder.Property(product => product.DeactivatedAt).HasColumnName("deactivated_at");

        builder.HasIndex(product => product.Name);
        builder.HasIndex(product => product.CategoryId);
        builder.HasQueryFilter(product => product.IsActive);
        builder.HasOne(product => product.Inventory)
            .WithOne()
            .HasForeignKey<Inventory>(inventory => inventory.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(product => product.Inventory).IsRequired();
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
