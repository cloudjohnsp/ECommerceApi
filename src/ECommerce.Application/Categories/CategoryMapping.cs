using ECommerce.Application.Categories.Dtos;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Categories;

internal static class CategoryMapping
{
    internal static CategoryDto ToDto(this Category category) => new(
        category.Id,
        category.Name,
        category.Slug,
        category.IsActive,
        category.CreatedAt,
        category.UpdatedAt);
}
