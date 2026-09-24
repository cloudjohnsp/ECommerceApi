using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.Name)
            .HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(category => category.Slug)
            .HasColumnName("slug").HasMaxLength(120).IsRequired();
        builder.Property(category => category.IsActive)
            .HasColumnName("is_active").IsRequired();
        builder.Property(category => category.CreatedAt)
            .HasColumnName("created_at").IsRequired();
        builder.Property(category => category.UpdatedAt).HasColumnName("updated_at");
        builder.Property(category => category.DeactivatedAt).HasColumnName("deactivated_at");
        builder.HasIndex(category => category.Slug).IsUnique();
        builder.HasQueryFilter(category => category.IsActive);
    }
}
