using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).ValueGeneratedNever();
        builder.Property(image => image.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(image => image.StorageKey)
            .HasColumnName("storage_key").HasMaxLength(500).IsRequired();
        builder.Property(image => image.Url)
            .HasColumnName("url").HasMaxLength(2048).IsRequired();
        builder.Property(image => image.FileName)
            .HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(image => image.ContentType)
            .HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(image => image.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(image => image.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(image => image.ProductId);
        builder.HasIndex(image => image.StorageKey).IsUnique();
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(image => image.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
