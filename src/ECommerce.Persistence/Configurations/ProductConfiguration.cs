using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).ValueGeneratedNever();

        builder.Property(product => product.Name)
            .HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(product => product.Description)
            .HasColumnName("description").HasMaxLength(2000).IsRequired();
        builder.Property(product => product.Price)
            .HasColumnName("price").HasPrecision(18, 2).IsRequired();
        builder.Property(product => product.Stock)
            .HasColumnName("stock").IsRequired();
        builder.Property(product => product.IsActive)
            .HasColumnName("is_active").IsRequired();
        builder.Property(product => product.CreatedAt)
            .HasColumnName("created_at").IsRequired();
        builder.Property(product => product.UpdatedAt).HasColumnName("updated_at");
        builder.Property(product => product.DeactivatedAt).HasColumnName("deactivated_at");

        builder.HasIndex(product => product.Name);
        builder.HasQueryFilter(product => product.IsActive);
    }
}
